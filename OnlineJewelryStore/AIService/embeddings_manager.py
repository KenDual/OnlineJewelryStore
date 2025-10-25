from sentence_transformers import SentenceTransformer
import faiss
import numpy as np
import pickle
import os
from typing import List, Dict, Tuple
import logging

logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)


class EmbeddingsManager:
    def __init__(self, model_name: str = 'sentence-transformers/all-MiniLM-L6-v2'):
        """
        Initialize embedding model
        
        Args:
            model_name: Sentence transformer model name
                       'all-MiniLM-L6-v2' - nhẹ, nhanh (384 dimensions)
        """
        self.model_name = model_name
        self.model = None
        self.index = None
        self.product_data = []
        self.dimension = 384  # Dimension của all-MiniLM-L6-v2
        
    def load_model(self):
        """Load sentence transformer model"""
        try:
            logger.info(f"Loading model: {self.model_name}")
            self.model = SentenceTransformer(self.model_name)
            logger.info("✅ Model loaded successfully")
            return True
        except Exception as e:
            logger.error(f"❌ Failed to load model: {e}")
            return False
    
    def create_product_text(self, product: Dict) -> str:
        """
        Tạo text representation của sản phẩm cho embedding
        
        Args:
            product: Product dictionary from database
            
        Returns:
            Formatted text string
        """
        # Kết hợp các thông tin quan trọng
        parts = []
        
        # Tên sản phẩm
        parts.append(f"Product: {product['ProductName']}")
        
        # Category
        if product.get('CategoryName'):
            parts.append(f"Category: {product['CategoryName']}")
        
        # Description
        if product.get('Description'):
            parts.append(f"Description: {product['Description']}")
        
        # Giá
        min_price = product.get('MinPrice', product.get('BasePrice', 0))
        max_price = product.get('MaxPrice', product.get('BasePrice', 0))
        
        if min_price == max_price:
            parts.append(f"Price: {min_price:,.0f} VND")
        else:
            parts.append(f"Price range: {min_price:,.0f} - {max_price:,.0f} VND")
        
        # Metals available
        if product.get('AvailableMetals'):
            parts.append(f"Materials: {product['AvailableMetals']}")
        
        # Stock status
        total_stock = product.get('TotalStock', 0)
        if total_stock > 0:
            parts.append("In stock")
        else:
            parts.append("Out of stock")
        
        # Reviews
        if product.get('ReviewCount', 0) > 0:
            avg_rating = product.get('AvgRating', 0)
            parts.append(f"Rating: {avg_rating:.1f}/5 ({product['ReviewCount']} reviews)")
        
        return " | ".join(parts)
    
    def build_index(self, products: List[Dict]) -> bool:
        """
        Build FAISS index from product data
        
        Args:
            products: List of product dictionaries
            
        Returns:
            Success status
        """
        if not self.model:
            logger.error("Model not loaded. Call load_model() first.")
            return False
        
        if not products:
            logger.error("No products provided")
            return False
        
        try:
            logger.info(f"Building index for {len(products)} products...")
            
            # Tạo text cho mỗi product
            texts = [self.create_product_text(p) for p in products]
            
            # Generate embeddings
            logger.info("Generating embeddings...")
            embeddings = self.model.encode(
                texts, 
                show_progress_bar=True,
                convert_to_numpy=True
            )
            
            # Normalize embeddings (để dùng inner product = cosine similarity)
            faiss.normalize_L2(embeddings)
            
            # Build FAISS index
            logger.info("Building FAISS index...")
            self.index = faiss.IndexFlatIP(self.dimension)  # Inner Product = Cosine sim với normalized vectors
            self.index.add(embeddings)
            
            # Store product data
            self.product_data = products
            
            logger.info(f"✅ Index built successfully with {self.index.ntotal} vectors")
            return True
            
        except Exception as e:
            logger.error(f"❌ Failed to build index: {e}")
            return False
    
    def search(
        self, 
        query: str, 
        top_k: int = 5,
        min_score: float = 0.3
    ) -> List[Tuple[Dict, float]]:
        """
        Search similar products using query
        
        Args:
            query: User search query
            top_k: Number of results to return
            min_score: Minimum similarity score (0-1)
            
        Returns:
            List of (product_dict, similarity_score) tuples
        """
        if not self.index:
            logger.error("Index not built. Call build_index() first.")
            return []
        
        try:
            # Generate query embedding
            query_embedding = self.model.encode([query], convert_to_numpy=True)
            faiss.normalize_L2(query_embedding)
            
            # Search
            scores, indices = self.index.search(query_embedding, top_k)
            
            # Filter by min_score và return kết quả
            results = []
            for score, idx in zip(scores[0], indices[0]):
                if score >= min_score:
                    results.append((self.product_data[idx], float(score)))
            
            logger.info(f"Found {len(results)} products matching query (score >= {min_score})")
            return results
            
        except Exception as e:
            logger.error(f"❌ Search failed: {e}")
            return []
    
    def save_index(self, filepath: str = "faiss_index"):
        """
        Save FAISS index and product data to disk
        
        Args:
            filepath: Base path for saving files (without extension)
        """
        if not self.index:
            logger.error("No index to save")
            return False
        
        try:
            # Save FAISS index
            faiss.write_index(self.index, f"{filepath}.index")
            
            # Save product data
            with open(f"{filepath}.pkl", 'wb') as f:
                pickle.dump(self.product_data, f)
            
            logger.info(f"✅ Index saved to {filepath}.index and {filepath}.pkl")
            return True
            
        except Exception as e:
            logger.error(f"❌ Failed to save index: {e}")
            return False
    
    def load_index(self, filepath: str = "faiss_index"):
        """
        Load FAISS index and product data from disk
        
        Args:
            filepath: Base path for loading files (without extension)
        """
        try:
            # Load FAISS index
            if not os.path.exists(f"{filepath}.index"):
                logger.error(f"Index file not found: {filepath}.index")
                return False
            
            self.index = faiss.read_index(f"{filepath}.index")
            
            # Load product data
            with open(f"{filepath}.pkl", 'rb') as f:
                self.product_data = pickle.load(f)
            
            logger.info(f"✅ Index loaded from {filepath}")
            logger.info(f"   - {self.index.ntotal} vectors")
            logger.info(f"   - {len(self.product_data)} products")
            return True
            
        except Exception as e:
            logger.error(f"❌ Failed to load index: {e}")
            return False


# ===== USAGE EXAMPLE =====
if __name__ == "__main__":
    from db_connector import DatabaseConnector
    
    # 1. Load products from database
    conn_string = "Driver={SQL Server};Server=localhost;Database=JewelryStoreDB;Trusted_Connection=yes;"
    db = DatabaseConnector(conn_string)
    
    if db.connect():
        products = db.get_all_products()
        db.disconnect()
        
        if products:
            # 2. Build embeddings and index
            em = EmbeddingsManager()
            em.load_model()
            em.build_index(products)
            
            # 3. Save index
            em.save_index("data/faiss_index")
            
            # 4. Test search
            results = em.search("nhẫn vàng giá rẻ", top_k=3)
            
            print("\n=== Search Results ===")
            for product, score in results:
                print(f"\nScore: {score:.3f}")
                print(f"Product: {product['ProductName']}")
                print(f"Price: {product.get('MinPrice', 0):,.0f} VND")
                print(f"Category: {product.get('CategoryName', 'N/A')}")