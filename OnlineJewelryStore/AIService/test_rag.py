"""
Test RAG Service
Script để test RAG pipeline hoàn chỉnh
"""

from embeddings_manager import EmbeddingsManager
from rag_service import RAGService
import logging

logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)


def test_rag_service():
    """Test full RAG pipeline"""
    
    print("\n" + "="*60)
    print("🧪 TESTING RAG SERVICE")
    print("="*60 + "\n")
    
    # ===== STEP 1: Load Index =====
    print("📂 Step 1: Loading FAISS index...")
    
    em = EmbeddingsManager()
    em.load_model()
    
    if not em.load_index("data/faiss_index"):
        logger.error("❌ Failed to load index. Run build_index.py first.")
        return False
    
    print(f"✅ Index loaded: {em.index.ntotal} products")
    
    # ===== STEP 2: Initialize RAG Service =====
    print("\n🤖 Step 2: Initializing RAG service...")
    
    rag = RAGService(
        embeddings_manager=em,
        ollama_url="http://localhost:11434",
        model_name="llama3.2:3b"
    )
    
    print("✅ RAG service initialized")
    
    # ===== STEP 3: Test Queries =====
    print("\n🔍 Step 3: Testing queries...")
    print("-" * 60)
    
    test_cases = [
        {
            "query": "I am looking for gold wedding rings around 15 million VND",
            "filters": {
                "min_price": 10000000,
                "max_price": 20000000
            }
        },
        {
            "query": "Is there any nice silver necklace?",
            "filters": {}
        },
        {
            "query": "I am looking for diamond jewelry for women",
            "filters": {
                "max_price": 50000000
            }
        }
    ]
    
    for idx, test in enumerate(test_cases, 1):
        print(f"\n{'='*60}")
        print(f"TEST CASE {idx}")
        print(f"{'='*60}")
        print(f"Query: {test['query']}")
        
        if test['filters']:
            print(f"Filters: {test['filters']}")
        
        print("\n⏳ Generating response...\n")
        
        result = rag.chat(
            user_query=test['query'],
            **test['filters']
        )
        
        if result['success']:
            print("✅ RESPONSE:")
            print("-" * 60)
            print(result['message'])
            print("-" * 60)
            
            if result['products']:
                print(f"\n📦 Suggested Products ({len(result['products'])}):")
                for p in result['products']:
                    print(f"   • {p['name']}")
                    print(f"     Price: {p['price']:,.0f} VND")
                    print(f"     Category: {p['category']}")
                    print(f"     Relevance: {p['score']:.3f}")
                    print()
        else:
            print(f"❌ ERROR: {result['message']}")
    
    # ===== SUCCESS =====
    print("\n" + "="*60)
    print("✅ RAG SERVICE TESTING COMPLETED")
    print("="*60)
    print("\n🎯 Next steps:")
    print("   1. If responses are good, move to Phase 3 (FastAPI integration)")
    print("   2. If responses need improvement, adjust prompts in rag_service.py")
    print("   3. To rebuild index with new products, run: python build_index.py")
    print()
    
    return True


def test_search_only():
    """Test search functionality only (no LLM)"""
    
    print("\n" + "="*60)
    print("🔍 TESTING SEARCH ONLY (No LLM)")
    print("="*60 + "\n")
    
    em = EmbeddingsManager()
    em.load_model()
    em.load_index("data/faiss_index")
    
    queries = [
        "gold ring",
        "silver necklace",
        "diamond jewelry",
        "men watch"
    ]
    
    for query in queries:
        print(f"\n📋 Query: '{query}'")
        results = em.search(query, top_k=5)
        
        if results:
            print(f"   Found {len(results)} results:\n")
            for idx, (product, score) in enumerate(results, 1):
                print(f"   {idx}. {product['ProductName']}")
                print(f"      Price: {product.get('MinPrice', 0):,.0f} VND")
                print(f"      Category: {product.get('CategoryName', 'N/A')}")
                print(f"      Score: {score:.3f}")
                print()
        else:
            print("   No results found")


def main():
    """Main test function"""
    import sys
    
    if len(sys.argv) > 1 and sys.argv[1] == "--search-only":
        return test_search_only()
    else:
        return test_rag_service()


if __name__ == "__main__":
    try:
        main()
    except KeyboardInterrupt:
        print("\n⚠️  Test interrupted")
    except Exception as e:
        logger.error(f"❌ Test failed: {e}")
        import traceback
        traceback.print_exc()