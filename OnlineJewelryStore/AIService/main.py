from fastapi import FastAPI, HTTPException
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel, Field
from typing import Optional, List, Dict
import logging
from datetime import datetime

# Import RAG components
from embeddings_manager import EmbeddingsManager
from rag_service import RAGService
from db_connector import DatabaseConnector

# Setup logging
logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(name)s - %(levelname)s - %(message)s'
)
logger = logging.getLogger(__name__)

# ===== PYDANTIC MODELS =====

class ProductInfo(BaseModel):
    """Product information in response"""
    id: int
    name: str
    price: float
    category: str
    image: Optional[str] = None
    score: float


class ChatRequest(BaseModel):
    """Request model for /chat endpoint"""
    message: str = Field(..., min_length=1, max_length=500, description="User message")
    category: Optional[str] = Field(None, description="Filter by category")
    min_price: Optional[float] = Field(None, ge=0, description="Minimum price")
    max_price: Optional[float] = Field(None, ge=0, description="Maximum price")
    conversation_history: Optional[List[Dict]] = Field(None, description="Chat history")
    
    class Config:
        json_schema_extra = {
            "example": {
                "message": "I want gold wedding rings around 15 million",
                "min_price": 10000000,
                "max_price": 20000000
            }
        }


class ChatResponse(BaseModel):
    """Response model for /chat endpoint"""
    success: bool
    message: str
    products: List[ProductInfo]
    timestamp: str


class HealthResponse(BaseModel):
    """Health check response"""
    status: str
    service: str
    timestamp: str
    index_loaded: bool
    total_products: int
    ollama_url: str


class RebuildResponse(BaseModel):
    """Index rebuild response"""
    success: bool
    message: str
    products_indexed: int
    timestamp: str


# ===== FASTAPI APP =====

app = FastAPI(
    title="AI Jewelry Advisor API",
    description="RAG-based AI chatbot for jewelry consultation",
    version="1.0.0"
)

# ===== CORS MIDDLEWARE =====

app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],  # For local development - restrict in production
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

# ===== GLOBAL STATE =====

class AppState:
    """Global application state"""
    embeddings_manager: Optional[EmbeddingsManager] = None
    rag_service: Optional[RAGService] = None
    initialized: bool = False
    connection_string: str = "Driver={SQL Server};Server=DESKTOP-195HJGO\\SQLEXPRESS;Database=OnlineJewelryStore;UID=sa;PWD=1;TrustServerCertificate=yes;"
    ollama_url: str = "http://localhost:11434"
    model_name: str = "llama3.2:3b"

state = AppState()


# ===== STARTUP EVENT =====

@app.on_event("startup")
async def startup_event():
    """Initialize services on startup"""
    logger.info("🚀 Starting AI Jewelry Advisor Service...")
    
    try:
        # Load embeddings manager
        logger.info("Loading embeddings manager...")
        state.embeddings_manager = EmbeddingsManager()
        state.embeddings_manager.load_model()
        
        # Load FAISS index
        logger.info("Loading FAISS index...")
        if not state.embeddings_manager.load_index("data/faiss_index"):
            logger.error("❌ Failed to load FAISS index")
            logger.warning("⚠️  Service started but index not loaded. Call /index-rebuild to build index.")
        else:
            logger.info(f"✅ Index loaded: {state.embeddings_manager.index.ntotal} products")
        
        # Initialize RAG service
        logger.info("Initializing RAG service...")
        state.rag_service = RAGService(
            embeddings_manager=state.embeddings_manager,
            ollama_url=state.ollama_url,
            model_name=state.model_name
        )
        
        state.initialized = True
        logger.info("✅ AI Jewelry Advisor Service started successfully!")
        
    except Exception as e:
        logger.error(f"❌ Startup failed: {e}")
        logger.warning("⚠️  Service started with errors. Some endpoints may not work.")


# ===== API ENDPOINTS =====

@app.get("/", tags=["Root"])
async def root():
    """Root endpoint"""
    return {
        "service": "AI Jewelry Advisor API",
        "version": "1.0.0",
        "status": "running",
        "endpoints": {
            "chat": "/chat",
            "health": "/health",
            "rebuild": "/index-rebuild",
            "docs": "/docs"
        }
    }


@app.get("/health", response_model=HealthResponse, tags=["Health"])
async def health_check():
    """
    Health check endpoint
    Returns service status and configuration
    """
    return HealthResponse(
        status="healthy" if state.initialized else "degraded",
        service="AI Jewelry Advisor",
        timestamp=datetime.now().isoformat(),
        index_loaded=state.embeddings_manager is not None and state.embeddings_manager.index is not None,
        total_products=state.embeddings_manager.index.ntotal if state.embeddings_manager and state.embeddings_manager.index else 0,
        ollama_url=state.ollama_url
    )


@app.post("/chat", response_model=ChatResponse, tags=["Chat"])
async def chat(request: ChatRequest):
    """
    Main chat endpoint
    
    Processes user message and returns AI response with product suggestions
    
    Args:
        request: ChatRequest with message and optional filters
        
    Returns:
        ChatResponse with AI message and suggested products
    """
    if not state.initialized or not state.rag_service:
        raise HTTPException(
            status_code=503,
            detail="Service not initialized. Please try again later or contact administrator."
        )
    
    if not state.embeddings_manager.index:
        raise HTTPException(
            status_code=503,
            detail="Product index not loaded. Please rebuild index using /index-rebuild endpoint."
        )
    
    try:
        logger.info(f"Processing chat request: {request.message[:50]}...")
        
        # Call RAG service
        result = state.rag_service.chat(
            user_query=request.message,
            category=request.category,
            min_price=request.min_price,
            max_price=request.max_price,
            conversation_history=request.conversation_history,
            top_k=3
        )
        
        if not result['success']:
            raise HTTPException(status_code=500, detail=result['message'])
        
        # Convert to response model
        products = [
            ProductInfo(
                id=p['id'],
                name=p['name'],
                price=p['price'],
                category=p['category'],
                image=p.get('image'),
                score=p['score']
            )
            for p in result['products']
        ]
        
        return ChatResponse(
            success=True,
            message=result['message'],
            products=products,
            timestamp=datetime.now().isoformat()
        )
        
    except HTTPException:
        raise
    except Exception as e:
        logger.error(f"❌ Chat error: {e}")
        raise HTTPException(
            status_code=500,
            detail=f"Failed to process request: {str(e)}"
        )


@app.post("/index-rebuild", response_model=RebuildResponse, tags=["Admin"])
async def rebuild_index():
    """
    Rebuild FAISS index from database
    
    This endpoint should be called:
    - After adding new products to database
    - When product information is updated
    - Periodically (e.g., daily) to ensure index is up-to-date
    
    Returns:
        RebuildResponse with rebuild status
    """
    if not state.embeddings_manager:
        raise HTTPException(
            status_code=503,
            detail="Embeddings manager not initialized"
        )
    
    try:
        logger.info("🔨 Starting index rebuild...")
        
        # Connect to database
        db = DatabaseConnector(state.connection_string)
        if not db.connect():
            raise Exception("Failed to connect to database")
        
        # Load products
        products = db.get_all_products()
        db.disconnect()
        
        if not products:
            raise Exception("No products found in database")
        
        logger.info(f"Loaded {len(products)} products from database")
        
        # Rebuild index
        if not state.embeddings_manager.build_index(products):
            raise Exception("Failed to build index")
        
        # Save index
        if not state.embeddings_manager.save_index("data/faiss_index"):
            raise Exception("Failed to save index")
        
        logger.info(f"✅ Index rebuilt successfully: {len(products)} products")
        
        return RebuildResponse(
            success=True,
            message=f"Index rebuilt successfully with {len(products)} products",
            products_indexed=len(products),
            timestamp=datetime.now().isoformat()
        )
        
    except Exception as e:
        logger.error(f"❌ Index rebuild failed: {e}")
        raise HTTPException(
            status_code=500,
            detail=f"Failed to rebuild index: {str(e)}"
        )


# ===== ERROR HANDLERS =====

@app.exception_handler(404)
async def not_found_handler(request, exc):
    """Handle 404 errors"""
    return {
        "error": "Not Found",
        "message": f"Endpoint {request.url.path} not found",
        "available_endpoints": ["/", "/health", "/chat", "/index-rebuild", "/docs"]
    }


@app.exception_handler(500)
async def internal_error_handler(request, exc):
    """Handle 500 errors"""
    logger.error(f"Internal server error: {exc}")
    return {
        "error": "Internal Server Error",
        "message": "An unexpected error occurred. Please try again later."
    }


# ===== RUN SERVER =====

if __name__ == "__main__":
    import uvicorn
    
    print("\n" + "="*60)
    print("🚀 STARTING AI JEWELRY ADVISOR SERVICE")
    print("="*60)
    print(f"📍 Server: http://localhost:8000")
    print(f"📚 Docs: http://localhost:8000/docs")
    print(f"🔍 Health: http://localhost:8000/health")
    print("="*60 + "\n")
    
    uvicorn.run(
        "main:app",
        host="0.0.0.0",
        port=8000,
        reload=True,
        log_level="info"
    )