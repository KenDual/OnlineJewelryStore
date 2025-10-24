"""
Initial Index Builder Script
Chạy script này để build FAISS index lần đầu tiên
"""

import os
import sys
from db_connector import DatabaseConnector
from embeddings_manager import EmbeddingsManager
import logging

logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)


def main():
    """Build initial FAISS index from database"""
    
    # ===== CONFIGURATION =====
    CONNECTION_STRING = "Driver={SQL Server};Server=DESKTOP-195HJGO\SQLEXPRESS;Database=OnlineJewelryStore;UID=sa;PWD=1;TrustServerCertificate=yes;"
    
    # Tạo thư mục data nếu chưa có
    os.makedirs("data", exist_ok=True)
    INDEX_PATH = "data/faiss_index"
    
    print("\n" + "="*60)
    print("🚀 BUILDING FAISS INDEX FOR JEWELRY STORE")
    print("="*60 + "\n")
    
    # ===== STEP 1: Connect to Database =====
    print("📊 Step 1: Connecting to SQL Server...")
    db = DatabaseConnector(CONNECTION_STRING)
    
    if not db.connect():
        logger.error("❌ Failed to connect to database. Please check connection string.")
        return False
    
    # ===== STEP 2: Load Products =====
    print("📦 Step 2: Loading products from database...")
    products = db.get_all_products()
    db.disconnect()
    
    if not products:
        logger.error("❌ No products found in database")
        return False
    
    print(f"✅ Loaded {len(products)} products")
    
    # Display sample
    if products:
        sample = products[0]
        print(f"\n📋 Sample product:")
        print(f"   - Name: {sample['ProductName']}")
        print(f"   - Category: {sample.get('CategoryName', 'N/A')}")
        print(f"   - Price: {sample.get('BasePrice', 0):,.0f} VND")
    
    # ===== STEP 3: Load Embedding Model =====
    print("\n🤖 Step 3: Loading sentence transformer model...")
    print("   (This may take a few minutes on first run)")
    
    em = EmbeddingsManager()
    if not em.load_model():
        logger.error("❌ Failed to load embedding model")
        return False
    
    # ===== STEP 4: Build FAISS Index =====
    print("\n🔨 Step 4: Building FAISS index...")
    print("   - Generating embeddings for all products")
    print("   - Creating vector index")
    
    if not em.build_index(products):
        logger.error("❌ Failed to build index")
        return False
    
    # ===== STEP 5: Save Index =====
    print(f"\n💾 Step 5: Saving index to {INDEX_PATH}...")
    
    if not em.save_index(INDEX_PATH):
        logger.error("❌ Failed to save index")
        return False
    
    # ===== STEP 6: Test Search =====
    print("\n🔍 Step 6: Testing search functionality...")
    
    test_queries = [
        "gold ring",
        "diamond jewelry",
        "silver necklace"
    ]
    
    for query in test_queries:
        results = em.search(query, top_k=3)
        
        print(f"\n   Query: '{query}'")
        if results:
            print(f"   ✅ Found {len(results)} results")
            top_result = results[0]
            print(f"      Top match: {top_result[0]['ProductName']} (score: {top_result[1]:.3f})")
        else:
            print(f"   ⚠️  No results found")
    
    # ===== SUCCESS =====
    print("\n" + "="*60)
    print("✅ INDEX BUILD COMPLETED SUCCESSFULLY!")
    print("="*60)
    print(f"\n📁 Files created:")
    print(f"   - {INDEX_PATH}.index")
    print(f"   - {INDEX_PATH}.pkl")
    print(f"\n📊 Statistics:")
    print(f"   - Total products indexed: {len(products)}")
    print(f"   - Vector dimension: {em.dimension}")
    print(f"   - Index size: {em.index.ntotal} vectors")
    print(f"\n🎯 Next steps:")
    print(f"   1. Verify files exist in 'data/' folder")
    print(f"   2. Test RAG service: python test_rag.py")
    print(f"   3. Start FastAPI server: uvicorn main:app --reload")
    print()
    
    return True


if __name__ == "__main__":
    try:
        success = main()
        sys.exit(0 if success else 1)
    except KeyboardInterrupt:
        print("\n⚠️  Build interrupted by user")
        sys.exit(1)
    except Exception as e:
        logger.error(f"❌ Unexpected error: {e}")
        import traceback
        traceback.print_exc()
        sys.exit(1)