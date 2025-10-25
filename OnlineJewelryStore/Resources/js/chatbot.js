(function ($) {
    'use strict';

    // Configuration
    const CONFIG = {
        API_BASE_URL: '/Chatbot',
        MAX_MESSAGE_LENGTH: 500,
        TYPING_DELAY: 800,
        AUTO_SCROLL_DELAY: 100,
        REQUEST_TIMEOUT: 180000  // ✅ 3 phút (180 giây)
    };

    // State Management
    let conversationHistory = [];
    let isProcessing = false;

    // DOM Elements Cache
    let $chatPopup, $chatMessages, $chatInput, $chatSendBtn,
        $typingIndicator, $toggleBtn, $closeBtn, $minimizeBtn;

    /**
     * Initialize chatbot on document ready
     */
    $(document).ready(function () {
        initializeElements();
        bindEvents();
        console.log('✅ AI Chatbot initialized');
    });

    /**
     * Cache DOM elements
     */
    function initializeElements() {
        $chatPopup = $('#chat-popup');
        $chatMessages = $('#chat-messages');
        $chatInput = $('#chat-input');
        $chatSendBtn = $('#chat-send-btn');
        $typingIndicator = $('#typing-indicator');
        $toggleBtn = $('#chat-toggle-btn');
        $closeBtn = $('#chat-close-btn');
        $minimizeBtn = $('#chat-minimize-btn');
    }

    /**
     * Bind all event handlers
     */
    function bindEvents() {
        // Toggle chat window
        $toggleBtn.on('click', toggleChat);
        $closeBtn.on('click', closeChat);
        $minimizeBtn.on('click', closeChat);

        // Send message
        $chatSendBtn.on('click', sendMessage);
        $chatInput.on('keypress', function (e) {
            if (e.which === 13 && !e.shiftKey) {
                e.preventDefault();
                sendMessage();
            }
        });

        // Quick action buttons
        $('.quick-action-btn').on('click', function () {
            const query = $(this).data('query');
            $chatInput.val(query);
            sendMessage();
        });

        // Prevent input when processing
        $chatInput.on('input', function () {
            const length = $(this).val().length;
            if (length > CONFIG.MAX_MESSAGE_LENGTH) {
                $(this).val($(this).val().substring(0, CONFIG.MAX_MESSAGE_LENGTH));
            }
        });
    }

    /**
     * Toggle chat popup visibility
     */
    function toggleChat() {
        if ($chatPopup.is(':visible')) {
            closeChat();
        } else {
            openChat();
        }
    }

    /**
     * Open chat popup
     */
    function openChat() {
        $chatPopup.fadeIn(300);
        $chatInput.focus();
        scrollToBottom();
    }

    /**
     * Close chat popup
     */
    function closeChat() {
        $chatPopup.fadeOut(300);
    }

    /**
     * Send user message
     */
    function sendMessage() {
        if (isProcessing) {
            console.warn('⏳ Already processing a message');
            return;
        }

        const message = $chatInput.val().trim();

        if (!message) {
            console.warn('⚠️ Empty message');
            return;
        }

        if (message.length > CONFIG.MAX_MESSAGE_LENGTH) {
            alert(`Message too long. Maximum ${CONFIG.MAX_MESSAGE_LENGTH} characters.`);
            return;
        }

        // Add user message to UI
        addUserMessage(message);

        // Clear input
        $chatInput.val('');

        // Call API
        callChatbotAPI(message);
    }

    /**
     * Call chatbot API endpoint
     */
    function callChatbotAPI(message) {
        isProcessing = true;
        $chatSendBtn.prop('disabled', true);
        showTypingIndicator();

        // ✅ FIXED - Proper format với Capital letters
        const requestData = {
            Message: message,
            Category: null,
            MinPrice: null,
            MaxPrice: null,
            ConversationHistory: conversationHistory
        };

        // ✅ LOG REQUEST để debug
        console.log('🚀 Sending request to:', CONFIG.API_BASE_URL + '/Chat');
        console.log('📝 Request data:', requestData);
        console.log('📋 JSON:', JSON.stringify(requestData, null, 2));

        $.ajax({
            url: CONFIG.API_BASE_URL + '/Chat',
            type: 'POST',
            contentType: 'application/json',
            data: JSON.stringify(requestData),
            timeout: CONFIG.REQUEST_TIMEOUT,  // ✅ 180 giây
            success: function (response) {
                console.log('✅ Response received:', response);
                handleSuccess(response, message);
            },
            error: function (xhr, status, error) {
                console.error('❌ Error:', { xhr, status, error });
                handleError(xhr, status, error);
            },
            complete: function () {
                console.log('🏁 Request complete');
                isProcessing = false;
                $chatSendBtn.prop('disabled', false);
                hideTypingIndicator();
            }
        });
    }

    /**
     * Handle successful API response
     */
    function handleSuccess(response, userMessage) {
        console.log('✅ API Response:', response);

        // ✅ FIXED - Update conversation history với Capital letters
        conversationHistory.push({
            Role: 'user',           // ✅ Capital R
            Content: userMessage    // ✅ Capital C
        });

        if (response.Success || response.success) {  // ✅ Support both formats
            conversationHistory.push({
                Role: 'assistant',          // ✅ Capital R
                Content: response.Message || response.message  // ✅ Capital C
            });

            // Add bot message
            addBotMessage(response.Message || response.message);

            // Add product cards if available
            const products = response.Products || response.products;
            if (products && products.length > 0) {
                addProductCards(products);
            }
        } else {
            // ✅ Show error message from response
            const errorMsg = response.Message || response.message || 'Xin lỗi, đã có lỗi xảy ra. Vui lòng thử lại sau.';
            addBotMessage(errorMsg);
            console.error('❌ API returned error:', response);
        }

        scrollToBottom();
    }

    /**
     * Handle API error
     */
    function handleError(xhr, status, error) {
        console.error('❌ API Error:', {
            status: status,
            error: error,
            statusCode: xhr.status,
            response: xhr.responseText
        });

        let errorMessage = 'Xin lỗi, hệ thống đang bận. Vui lòng thử lại sau.';

        if (status === 'timeout') {
            errorMessage = '⏰ Yêu cầu hết thời gian chờ (3 phút). Lần đầu có thể cần 2-3 phút để load model. Vui lòng thử lại.';
        } else if (xhr.status === 500) {
            errorMessage = '❌ Lỗi server (500). Vui lòng kiểm tra logs hoặc liên hệ admin.';
        } else if (xhr.status === 0) {
            errorMessage = '🔌 Không thể kết nối đến server. Kiểm tra xem FastAPI có đang chạy tại http://localhost:8000 không.';
        } else if (xhr.status === 404) {
            errorMessage = '🔍 Không tìm thấy endpoint /Chatbot/Chat. Kiểm tra routing.';
        }

        // Try parse error from response
        try {
            const errorResponse = JSON.parse(xhr.responseText);
            if (errorResponse.Message || errorResponse.message) {
                errorMessage = errorResponse.Message || errorResponse.message;
            }
        } catch (e) {
            // Use default message
        }

        addBotMessage(errorMessage);
        scrollToBottom();
    }

    /**
     * Add user message to chat
     */
    function addUserMessage(text) {
        const messageHtml = `
            <div class="chat-message user-message">
                <div class="message-avatar">
                    <i class="fa fa-user"></i>
                </div>
                <div class="message-content">
                    <div class="message-bubble">${escapeHtml(text)}</div>
                    <div class="message-time">${getCurrentTime()}</div>
                </div>
            </div>
        `;

        $chatMessages.append(messageHtml);
        scrollToBottom();
    }

    /**
     * Add bot message to chat
     */
    function addBotMessage(text) {
        const messageHtml = `
            <div class="chat-message bot-message">
                <div class="message-avatar">
                    <i class="fa fa-robot"></i>
                </div>
                <div class="message-content">
                    <div class="message-bubble">${escapeHtml(text)}</div>
                    <div class="message-time">${getCurrentTime()}</div>
                </div>
            </div>
        `;

        $chatMessages.append(messageHtml);
    }

    /**
     * Add product cards to chat
     */
    function addProductCards(products) {
        products.forEach(function (product) {
            // ✅ Support both capital and lowercase property names
            const productId = product.Id || product.id;
            const productName = product.Name || product.name;
            const productPrice = product.Price || product.price;
            const productCategory = product.Category || product.category;
            const productImage = product.Image || product.image;
            const productScore = product.Score || product.score;
            const productUrl = product.ProductUrl || product.productUrl || `/Shop/Details/${productId}`;

            const cardHtml = `
                <div class="chat-message bot-message">
                    <div class="message-avatar">
                        <i class="fa fa-gem"></i>
                    </div>
                    <div class="message-content">
                        <div class="product-card-chat" onclick="window.location.href='${productUrl}'">
                            <div class="product-card-content">
                                <img src="${productImage || '/Content/images/no-image.png'}" 
                                     alt="${productName}" 
                                     class="product-card-image"
                                     onerror="this.src='/Content/images/no-image.png'">
                                <div class="product-card-info">
                                    <div class="product-card-title">${escapeHtml(productName)}</div>
                                    <div class="product-card-price">${formatPrice(productPrice)}</div>
                                    <div class="product-card-category">
                                        <i class="fa fa-tag"></i> ${escapeHtml(productCategory)}
                                    </div>
                                    <span class="product-card-score">
                                        Match: ${Math.round(productScore * 100)}%
                                    </span>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>
            `;

            $chatMessages.append(cardHtml);
        });
    }

    /**
     * Show typing indicator
     */
    function showTypingIndicator() {
        $typingIndicator.fadeIn(200);
        scrollToBottom();
    }

    /**
     * Hide typing indicator
     */
    function hideTypingIndicator() {
        $typingIndicator.fadeOut(200);
    }

    /**
     * Scroll chat to bottom
     */
    function scrollToBottom() {
        setTimeout(function () {
            $chatMessages.animate({
                scrollTop: $chatMessages[0].scrollHeight
            }, 300);
        }, CONFIG.AUTO_SCROLL_DELAY);
    }

    /**
     * Get current time formatted
     */
    function getCurrentTime() {
        const now = new Date();
        return now.toLocaleTimeString('vi-VN', {
            hour: '2-digit',
            minute: '2-digit'
        });
    }

    /**
     * Format price to VND
     */
    function formatPrice(price) {
        return new Intl.NumberFormat('vi-VN', {
            style: 'currency',
            currency: 'VND'
        }).format(price);
    }

    /**
     * Escape HTML to prevent XSS
     */
    function escapeHtml(text) {
        const map = {
            '&': '&amp;',
            '<': '&lt;',
            '>': '&gt;',
            '"': '&quot;',
            "'": '&#039;'
        };
        return text.replace(/[&<>"']/g, function (m) {
            return map[m];
        });
    }

})(jQuery);