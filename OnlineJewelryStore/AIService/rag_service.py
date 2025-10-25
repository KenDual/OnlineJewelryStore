"""
RAG Service - Core Pipeline OPTIMIZED
Kết hợp vector search với Llama LLM để tạo AI advisor
"""

import requests
import json
import time
from typing import List, Dict, Optional, Tuple
import logging

logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)


class RAGService:
    def __init__(
        self, 
        embeddings_manager,
        ollama_url: str = "http://localhost:11434",
        model_name: str = "llama3.2:3b"
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
        # Vector search - lấy nhiều để filter
        results = self.em.search(query, top_k=min(top_k * 3, 15))
        
        # Apply filters
        filtered_results = []
        for product, score in results:
            # Skip low score results
            if score < 0.3:
                continue
                
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
            
            # Break early if enough results
            if len(filtered_results) >= top_k:
                break
        
        logger.info(f"Found {len(filtered_results)}/{len(results)} matching products")
        return filtered_results[:top_k]
    
    def generate_context(self, products: List[Tuple[Dict, float]]) -> str:
        """
        Generate context từ search results - VERSION NGẮN GỌN
        """
        if not products:
            return "Không tìm thấy sản phẩm phù hợp."
    
        context_parts = []
        context_parts.append(f"Tìm thấy {len(products)} sản phẩm:")
        context_parts.append("")
        
        for idx, (product, score) in enumerate(products, 1):
            product_info = []
            
            # Basic info (1 line)
            name = product['ProductName']
            product_id = product['ProductID']
            product_info.append(f"{idx}. {name} (ID: {product_id})")
            
            # Price (1 line)
            min_price = product.get('MinPrice', product.get('BasePrice', 0))
            product_info.append(f"   Giá: {min_price:,.0f} VND")
            
            # Category (1 line)
            category = product.get('CategoryName', 'N/A')
            product_info.append(f"   Danh mục: {category}")
            
            # Description (1 line - RÚT GỌN 60 chars)
            desc = product.get('Description', '')
            if desc:
                short_desc = desc[:60] + '...' if len(desc) > 60 else desc
                product_info.append(f"   Mô tả: {short_desc}")
            
            # Materials (1 line)
            metals = product.get('AvailableMetals', '')
            if metals:
                # Rút gọn materials
                metals_list = metals.split(',')[:2]  # Chỉ lấy 2 loại đầu
                product_info.append(f"   Chất liệu: {', '.join(metals_list)}")
            
            # Stock (inline)
            stock = product.get('TotalStock', 0)
            stock_text = "Còn hàng" if stock > 0 else "Hết hàng"
            product_info.append(f"   Tình trạng: {stock_text}")
            
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
        Tạo prompt NGẮN GỌN cho Llama
        """
        # System prompt siêu ngắn
        system_prompt = """Bạn là tư vấn viên trang sức chuyên nghiệp.
NHIỆM VỤ: Gợi ý 1-2 sản phẩm PHÙ HỢP từ danh sách.
QUY TẮC: 
- CHỈ dùng thông tin có sẵn
- Trả lời NGẮN GỌN (3-4 câu)
- Format: Chào → Gợi ý (Tên, ID, Giá, Lý do)"""
        
        prompt_parts = [
            system_prompt,
            "\n--- SẢN PHẨM ---",
            context,
            "\n--- KHÁCH HỎI ---",
            user_query,
            "\n--- TRẢ LỜI ---"
        ]
    
        # History - CHỈ 2 tin gần nhất, mỗi tin max 80 chars
        if conversation_history and len(conversation_history) > 0:
            history_text = "\n--- LỊCH SỬ ---\n"
            for msg in conversation_history[-2:]:
                role = "K" if msg.get('role') == 'user' else "B"
                content = msg.get('content', '')[:80]
                history_text += f"{role}: {content}\n"
            
            prompt_parts.insert(-2, history_text)
    
        return "\n".join(prompt_parts)
    
    def call_llama(
        self, 
        prompt: str, 
        max_tokens: int = 120,  # Giảm từ 400 → 200
        temperature: float = 0.3  # Giảm từ 0.5 → 0.4
    ) -> Optional[str]:
        """
        Call Ollama API để generate response - OPTIMIZED
        
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
                    "top_p": 0.75,
                    "top_k": 15,
                    "num_ctx": 768,
                    "repeat_penalty": 1.15,
                    "num_gpu": 0,
                    "num_thread": 6
                }
            }
            
            logger.info(f"🤖 Calling Ollama (max_tokens={max_tokens}, timeout=120s)")
            start_time = time.time()
            
            response = requests.post(
                self.api_endpoint,
                json=payload,
                timeout=120  # ✅ TĂNG TỪ 45 → 120 giây
            )
            
            if response.status_code == 200:
                result = response.json()
                generated_text = result.get('response', '')
                
                # Log performance metrics
                total_duration = result.get('total_duration', 0) / 1e9
                eval_count = result.get('eval_count', 0)
                eval_duration = result.get('eval_duration', 0) / 1e9
                
                if eval_duration > 0:
                    tokens_per_sec = eval_count / eval_duration
                    logger.info(f"✅ Generated {eval_count} tokens in {eval_duration:.2f}s ({tokens_per_sec:.1f} tok/s)")
                else:
                    logger.info(f"✅ Response generated in {total_duration:.2f}s")
                
                return generated_text
            else:
                logger.error(f"❌ Ollama error: {response.status_code}")
                logger.error(response.text)
                return None
                
        except requests.exceptions.Timeout:
            elapsed = time.time() - start_time
            logger.error(f"❌ Ollama timeout after {elapsed:.1f}s")
            logger.error("💡 Tip: First call may take 60-90s to load model. Try again.")
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
        Main chat function - RAG pipeline OPTIMIZED
        
        Args:
            user_query: User question
            category, min_price, max_price: Optional filters
            conversation_history: Chat history
            top_k: Number of products to retrieve
            
        Returns:
            Dictionary với response và metadata
        """
        start_time = time.time()
        logger.info(f"🔍 Query: {user_query}")
        
        # 1. Search relevant products
        t1 = time.time()
        products = self.search_products(
            query=user_query,
            category=category,
            min_price=min_price,
            max_price=max_price,
            top_k=top_k
        )
        logger.info(f"⏱️  Search: {time.time()-t1:.2f}s")
        
        # 2. Generate context
        t2 = time.time()
        context = self.generate_context(products)
        logger.info(f"⏱️  Context: {time.time()-t2:.2f}s")
        
        # 3. Create prompt
        t3 = time.time()
        prompt = self.create_prompt(user_query, context, conversation_history)
        logger.info(f"⏱️  Prompt: {time.time()-t3:.2f}s | Length: {len(prompt)} chars")
        
        # 4. Call Llama (main bottleneck)
        t4 = time.time()
        response = self.call_llama(
            prompt,
            max_tokens=120,      # Giảm output
            temperature=0.3      # Faster
        )
        llama_time = time.time() - t4
        logger.info(f"⏱️  Llama: {llama_time:.2f}s")
        
        total_time = time.time() - start_time
        logger.info(f"✅ Total: {total_time:.2f}s")
        
        if not response:
            return {
                "success": False,
                "message": "Xin lỗi, hệ thống đang bận. Vui lòng thử lại sau ít phút.",
                "products": []
            }
        
        # 5. Return results
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