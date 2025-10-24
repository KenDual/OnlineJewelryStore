"""
RAG Service - Core Pipeline
Kết hợp vector search với Llama LLM để tạo AI advisor
"""

import requests
import json
from typing import List, Dict, Optional, Tuple
import logging

logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)


class RAGService:
    def __init__(
        self, 
        embeddings_manager,
        ollama_url: str = "http://localhost:11434",
        model_name: str = "llama3.1:8b"
    ):
        """
        Initialize RAG Service
        
        Args:
            embeddings_manager: EmbeddingsManager instance với loaded index
            ollama_url: Ollama API endpoint
            model_name: Llama model name
        """
        self.em = embeddings_manager
        self.ollama_url = ollama_url
        self.model_name = model_name
        self.api_endpoint = f"{ollama_url}/api/generate"
    
    def search_products(
        self,
        query: str,
        category: Optional[str] = None,
        min_price: Optional[float] = None,
        max_price: Optional[float] = None,
        top_k: int = 5
    ) -> List[Tuple[Dict, float]]:
        """
        Search products với filters
        
        Args:
            query: User query
            category: Tên category (optional)
            min_price: Giá tối thiểu (optional)
            max_price: Giá tối đa (optional)
            top_k: Số lượng kết quả
            
        Returns:
            List of (product, score) tuples
        """
        # Vector search
        results = self.em.search(query, top_k=top_k * 2)  # Lấy nhiều hơn để filter
        
        # Apply filters
        filtered_results = []
        for product, score in results:
            # Filter by category
            if category and product.get('CategoryName', '').lower() != category.lower():
                continue
            
            # Filter by price
            product_price = product.get('MinPrice', product.get('BasePrice', 0))
            
            if min_price and product_price < min_price:
                continue
            
            if max_price and product_price > max_price:
                continue
            
            filtered_results.append((product, score))
            
            if len(filtered_results) >= top_k:
                break
        
        logger.info(f"Search returned {len(filtered_results)} products after filtering")
        return filtered_results
    
    def generate_context(self, products: List[Tuple[Dict, float]]) -> str:
        """
        Tạo context string từ search results
        
        Args:
            products: List of (product, score) tuples
            
        Returns:
            Formatted context string
        """
        if not products:
            return "Không tìm thấy sản phẩm phù hợp trong database."
        
        context_parts = []
        context_parts.append("=== SẢN PHẨM CÓ SẴN ===\n")
        
        for idx, (product, score) in enumerate(products, 1):
            product_info = [
                f"[Sản phẩm {idx}]",
                f"- Tên: {product['ProductName']}",
                f"- ID: {product['ProductID']}",
                f"- Danh mục: {product.get('CategoryName', 'N/A')}",
            ]
            
            # Giá
            min_price = product.get('MinPrice', product.get('BasePrice', 0))
            max_price = product.get('MaxPrice', product.get('BasePrice', 0))
            
            if min_price == max_price:
                product_info.append(f"- Giá: {min_price:,.0f} VND")
            else:
                product_info.append(f"- Giá: {min_price:,.0f} - {max_price:,.0f} VND")
            
            # Description (truncate nếu quá dài)
            desc = product.get('Description', '')
            if desc:
                if len(desc) > 150:
                    desc = desc[:150] + "..."
                product_info.append(f"- Mô tả: {desc}")
            
            # Metals
            if product.get('AvailableMetals'):
                product_info.append(f"- Chất liệu: {product['AvailableMetals']}")
            
            # Stock
            stock = product.get('TotalStock', 0)
            stock_status = "Còn hàng" if stock > 0 else "Hết hàng"
            product_info.append(f"- Trạng thái: {stock_status}")
            
            # Reviews
            if product.get('ReviewCount', 0) > 0:
                rating = product.get('AvgRating', 0)
                count = product.get('ReviewCount', 0)
                product_info.append(f"- Đánh giá: {rating:.1f}⭐ ({count} reviews)")
            
            # Relevance score
            product_info.append(f"- Độ phù hợp: {score:.2f}")
            
            context_parts.append("\n".join(product_info))
            context_parts.append("")  # Empty line
        
        return "\n".join(context_parts)
    
    def create_prompt(
        self, 
        user_query: str, 
        context: str,
        conversation_history: Optional[List[Dict]] = None
    ) -> str:
        """
        Tạo prompt cho Llama model
        
        Args:
            user_query: User's question
            context: Product context from RAG
            conversation_history: Optional chat history
            
        Returns:
            Formatted prompt
        """
        system_prompt = """Bạn là tư vấn viên chuyên nghiệp của cửa hàng trang sức trực tuyến.

NHIỆM VỤ:
- Tư vấn sản phẩm dựa TRÊN dữ liệu có sẵn
- Gợi ý 1-3 sản phẩm PHÙ HỢP NHẤT với nhu cầu khách hàng
- Giải thích TẠI SAO sản phẩm phù hợp
- Trả lời bằng tiếng Việt, thân thiện và chuyên nghiệp

GIỚI HẠN:
- CHỈ tư vấn về sản phẩm có trong danh sách được cung cấp
- KHÔNG bịa đặt thông tin sản phẩm không có trong database
- KHÔNG tư vấn về đặt hàng, thanh toán, vận chuyển
- Nếu không tìm thấy sản phẩm phù hợp, lịch sự từ chối và gợi ý tìm kiếm khác

FORMAT TRẢ LỜI:
1. Chào hỏi ngắn gọn
2. Phân tích nhu cầu của khách
3. Gợi ý 1-3 sản phẩm với: Tên, Giá, Lý do phù hợp, ID sản phẩm
4. Kết thúc với câu hỏi mở (nếu cần làm rõ thêm)

VÍ DỤ OUTPUT:
"Dạ, em xin chào anh/chị!

Anh/chị đang tìm nhẫn cưới vàng trong khoảng giá 10-20 triệu. Em xin giới thiệu 2 sản phẩm phù hợp:

🔹 **Nhẫn Cưới Vàng 18K Classic** (ID: 123)
   - Giá: 15,500,000 VND
   - Thiết kế đơn giản, thanh lịch, phù hợp ngày cưới
   - Vàng 18K bền đẹp, không bị phai màu
   - Đang còn hàng, được khách hàng đánh giá 4.8⭐

🔹 **Nhẫn Cưới Vàng Trắng Sang Trọng** (ID: 145)
   - Giá: 18,200,000 VND  
   - Kiểu dáng hiện đại, sang trọng
   - Vàng trắng 18K cao cấp
   - Còn hàng

Anh/chị thích kiểu thiết kế đơn giản hay có điểm nhấn đá quý ạ?"
"""
        
        prompt_parts = [
            system_prompt,
            "\n=== DỮ LIỆU SẢN PHẨM ===",
            context,
            "\n=== CÂU HỎI CỦA KHÁCH HÀNG ===",
            f"User: {user_query}",
            "\n=== TRẢ LỜI CỦA TƯ VẤN VIÊN ===",
            "Assistant:"
        ]
        
        # Thêm conversation history nếu có
        if conversation_history:
            history_text = "\n=== LỊCH SỬ TRÒ CHUYỆN ===\n"
            for msg in conversation_history[-3:]:  # Chỉ lấy 3 tin nhắn gần nhất
                role = msg.get('role', 'user')
                content = msg.get('content', '')
                history_text += f"{role}: {content}\n"
            
            # Insert history before current query
            prompt_parts.insert(-3, history_text)
        
        return "\n".join(prompt_parts)
    
    def call_llama(
        self, 
        prompt: str, 
        max_tokens: int = 800,
        temperature: float = 0.7
    ) -> Optional[str]:
        """
        Call Ollama API để generate response
        
        Args:
            prompt: Full prompt string
            max_tokens: Max response length
            temperature: Creativity level (0-1)
            
        Returns:
            Generated response text hoặc None nếu lỗi
        """
        try:
            payload = {
                "model": self.model_name,
                "prompt": prompt,
                "stream": False,
                "options": {
                    "num_predict": max_tokens,
                    "temperature": temperature,
                    "top_p": 0.9,
                    "top_k": 40
                }
            }
            
            logger.info(f"Calling Ollama API: {self.api_endpoint}")
            
            response = requests.post(
                self.api_endpoint,
                json=payload,
                timeout=60  # 60 giây timeout
            )
            
            if response.status_code == 200:
                result = response.json()
                generated_text = result.get('response', '')
                logger.info(f"✅ Llama response generated ({len(generated_text)} chars)")
                return generated_text
            else:
                logger.error(f"❌ Ollama API error: {response.status_code}")
                logger.error(response.text)
                return None
                
        except requests.exceptions.Timeout:
            logger.error("❌ Ollama API timeout")
            return None
        except Exception as e:
            logger.error(f"❌ Error calling Ollama: {e}")
            return None
    
    def chat(
        self,
        user_query: str,
        category: Optional[str] = None,
        min_price: Optional[float] = None,
        max_price: Optional[float] = None,
        conversation_history: Optional[List[Dict]] = None,
        top_k: int = 3
    ) -> Dict:
        """
        Main chat function - RAG pipeline
        
        Args:
            user_query: User question
            category, min_price, max_price: Optional filters
            conversation_history: Chat history
            top_k: Number of products to retrieve
            
        Returns:
            Dictionary với response và metadata
        """
        logger.info(f"Processing query: {user_query}")
        
        # 1. Search relevant products
        products = self.search_products(
            query=user_query,
            category=category,
            min_price=min_price,
            max_price=max_price,
            top_k=top_k
        )
        
        # 2. Generate context
        context = self.generate_context(products)
        
        # 3. Create prompt
        prompt = self.create_prompt(user_query, context, conversation_history)
        
        # 4. Call Llama
        response = self.call_llama(prompt)
        
        if not response:
            return {
                "success": False,
                "message": "Xin lỗi, hệ thống đang gặp sự cố. Vui lòng thử lại sau.",
                "products": []
            }
        
        # 5. Extract product IDs from response (optional)
        product_ids = [p[0]['ProductID'] for p in products[:3]]
        
        return {
            "success": True,
            "message": response,
            "products": [
                {
                    "id": p[0]['ProductID'],
                    "name": p[0]['ProductName'],
                    "price": p[0].get('MinPrice', p[0].get('BasePrice', 0)),
                    "category": p[0].get('CategoryName', ''),
                    "image": p[0].get('MainImageURL', ''),
                    "score": p[1]
                }
                for p in products[:3]
            ]
        }


# ===== USAGE EXAMPLE =====
if __name__ == "__main__":
    from embeddings_manager import EmbeddingsManager
    
    # 1. Load embeddings manager
    em = EmbeddingsManager()
    em.load_model()
    em.load_index("data/faiss_index")
    
    # 2. Initialize RAG service
    rag = RAGService(em)
    
    # 3. Test chat
    result = rag.chat(
        user_query="Tôi muốn tìm nhẫn cưới vàng giá khoảng 15 triệu",
        min_price=10000000,
        max_price=20000000
    )
    
    print("\n=== RAG Response ===")
    print(result['message'])
    
    print("\n=== Suggested Products ===")
    for p in result['products']:
        print(f"- {p['name']} ({p['price']:,.0f} VND)")