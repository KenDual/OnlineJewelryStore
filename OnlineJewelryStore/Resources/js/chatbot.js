(function ($) {
    'use strict';

    // Configuration
    const CONFIG = {
        API_BASE_URL: '/Chatbot',
        MAX_MESSAGE_LENGTH: 500,
        TYPING_DELAY: 800,
        AUTO_SCROLL_DELAY: 100,
        REQUEST_TIMEOUT: 60000
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

        const requestData = {
            Message: message,
            Category: null,
            MinPrice: null,
            MaxPrice: null,
            ConversationHistory: conversationHistory
        };

        $.ajax({
            url: CONFIG.API_BASE_URL + '/Chat',
            type: 'POST',
            contentType: 'application/json',
            data: JSON.stringify(requestData),
            timeout: 60000, // 60 trên lap, trên PC chạy GPU nên chỉnh lại 30s thôi
            success: function (response) {
                handleSuccess(response, message);
            },
            error: function (xhr, status, error) {
                handleError(xhr, status, error);
            },
            complete: function () {
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

        // Update conversation history
        conversationHistory.push({
            role: 'user',
            content: userMessage
        });

        if (response.success) {
            conversationHistory.push({
                role: 'assistant',
                content: response.message
            });

            // Add bot message
            addBotMessage(response.message);

            // Add product cards if available
            if (response.products && response.products.length > 0) {
                addProductCards(response.products);
            }
        } else {
            addBotMessage('Xin lỗi, đã có lỗi xảy ra. Vui lòng thử lại sau.');
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
            response: xhr.responseText
        });

        let errorMessage = 'Xin lỗi, hệ thống đang bận. Vui lòng thử lại sau.';

        if (status === 'timeout') {
            errorMessage = 'Yêu cầu hết thời gian chờ. Vui lòng thử lại.';
        } else if (xhr.status === 500) {
            errorMessage = 'Lỗi server. Vui lòng liên hệ quản trị viên.';
        } else if (xhr.status === 0) {
            errorMessage = 'Không thể kết nối đến server. Kiểm tra kết nối mạng.';
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
            const cardHtml = `
                <div class="chat-message bot-message">
                    <div class="message-avatar">
                        <i class="fa fa-gem"></i>
                    </div>
                    <div class="message-content">
                        <div class="product-card-chat" onclick="window.location.href='/Products/Details/${product.id}'">
                            <div class="product-card-content">
                                <img src="${product.image || '/Content/images/no-image.png'}" 
                                     alt="${product.name}" 
                                     class="product-card-image"
                                     onerror="this.src='/Content/images/no-image.png'">
                                <div class="product-card-info">
                                    <div class="product-card-title">${escapeHtml(product.name)}</div>
                                    <div class="product-card-price">${formatPrice(product.price)}</div>
                                    <div class="product-card-category">
                                        <i class="fa fa-tag"></i> ${escapeHtml(product.category)}
                                    </div>
                                    <span class="product-card-score">
                                        Match: ${Math.round(product.score * 100)}%
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