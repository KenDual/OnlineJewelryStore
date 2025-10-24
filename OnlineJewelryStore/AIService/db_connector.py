import pyodbc
from typing import List, Dict, Optional
import logging

logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)


class DatabaseConnector:
    def __init__(self, connection_string: str):
        """
        Initialize database connection
        
        Args:
            connection_string: SQL Server connection string từ web.config
            Format: "Driver={SQL Server};Server=DESKTOP-195HJGO\SQLEXPRESS;Database=OnlineJewelryStore;UID=sa;PWD=1;TrustServerCertificate=yes;"
        """
        self.connection_string = connection_string
        self.conn = None
        
    def connect(self):
        """Establish database connection"""
        try:
            self.conn = pyodbc.connect(self.connection_string)
            logger.info("✅ Database connected successfully")
            return True
        except Exception as e:
            logger.error(f"❌ Database connection failed: {e}")
            return False
    
    def disconnect(self):
        """Close database connection"""
        if self.conn:
            self.conn.close()
            logger.info("Database connection closed")
    
    def get_all_products(self) -> List[Dict]:
        """
        Lấy tất cả sản phẩm với thông tin đầy đủ cho RAG indexing
        
        Returns:
            List of product dictionaries
        """
        query = """
        SELECT 
            p.ProductID,
            p.ProductName,
            p.Description,
            p.BasePrice,
            p.CategoryID,
            p.IsActive,
            c.CategoryName,
            c.ParentCategoryID,
            
            -- Lấy thông tin variants
            STRING_AGG(CAST(pv.MetalType AS NVARCHAR(MAX)), ', ') as AvailableMetals,
            MIN(pv.AdditionalPrice) as MinAdditionalPrice,
            MAX(pv.AdditionalPrice) as MaxAdditionalPrice,
            SUM(pv.StockQuantity) as TotalStock,
            
            -- Lấy URL ảnh chính
            (SELECT TOP 1 URL FROM ProductMedia 
             WHERE ProductID = p.ProductID AND IsMain = 1) as MainImageURL,
            
            -- Đếm số reviews và rating trung bình
            COUNT(DISTINCT r.ReviewID) as ReviewCount,
            AVG(CAST(r.Rating AS FLOAT)) as AvgRating
            
        FROM Products p
        INNER JOIN Categories c ON p.CategoryID = c.CategoryID
        LEFT JOIN ProductVariants pv ON p.ProductID = pv.ProductID
        LEFT JOIN Reviews r ON p.ProductID = r.ProductID
        
        WHERE p.IsActive = 1
        
        GROUP BY 
            p.ProductID, p.ProductName, p.Description, 
            p.BasePrice, p.CategoryID, p.IsActive,
            c.CategoryName, c.ParentCategoryID
        
        ORDER BY p.ProductID
        """
        
        try:
            cursor = self.conn.cursor()
            cursor.execute(query)
            
            columns = [column[0] for column in cursor.description]
            products = []
            
            for row in cursor.fetchall():
                product = dict(zip(columns, row))
                # Tính giá thực tế
                product['MinPrice'] = product['BasePrice'] + (product['MinAdditionalPrice'] or 0)
                product['MaxPrice'] = product['BasePrice'] + (product['MaxAdditionalPrice'] or 0)
                products.append(product)
            
            logger.info(f"✅ Retrieved {len(products)} products from database")
            return products
            
        except Exception as e:
            logger.error(f"❌ Error querying products: {e}")
            return []
    
    def get_product_by_id(self, product_id: int) -> Optional[Dict]:
        """Lấy chi tiết 1 sản phẩm theo ID"""
        query = """
        SELECT 
            p.ProductID,
            p.ProductName,
            p.Description,
            p.BasePrice,
            c.CategoryName
        FROM Products p
        INNER JOIN Categories c ON p.CategoryID = c.CategoryID
        WHERE p.ProductID = ? AND p.IsActive = 1
        """
        
        try:
            cursor = self.conn.cursor()
            cursor.execute(query, (product_id,))
            row = cursor.fetchone()
            
            if row:
                columns = [column[0] for column in cursor.description]
                return dict(zip(columns, row))
            return None
            
        except Exception as e:
            logger.error(f"❌ Error querying product {product_id}: {e}")
            return None
    
    def search_products(
        self, 
        category_id: Optional[int] = None,
        min_price: Optional[float] = None,
        max_price: Optional[float] = None,
        metal_type: Optional[str] = None,
        in_stock_only: bool = True
    ) -> List[Dict]:
        """
        Tìm kiếm sản phẩm với filters
        
        Args:
            category_id: Lọc theo danh mục
            min_price: Giá tối thiểu
            max_price: Giá tối đa
            metal_type: Loại kim loại (Gold, Platinum, Silver, Rose Gold)
            in_stock_only: Chỉ lấy sản phẩm còn hàng
        """
        query = """
        SELECT DISTINCT
            p.ProductID,
            p.ProductName,
            p.Description,
            p.BasePrice,
            c.CategoryName,
            MIN(pv.AdditionalPrice) as MinAdditionalPrice,
            SUM(pv.StockQuantity) as TotalStock
        FROM Products p
        INNER JOIN Categories c ON p.CategoryID = c.CategoryID
        LEFT JOIN ProductVariants pv ON p.ProductID = pv.ProductID
        WHERE p.IsActive = 1
        """
        
        params = []
        
        # Thêm filters
        if category_id:
            query += " AND p.CategoryID = ?"
            params.append(category_id)
        
        if metal_type:
            query += " AND pv.MetalType = ?"
            params.append(metal_type)
        
        query += """
        GROUP BY 
            p.ProductID, p.ProductName, p.Description, 
            p.BasePrice, c.CategoryName
        HAVING 1=1
        """
        
        if min_price:
            query += " AND (p.BasePrice + MIN(pv.AdditionalPrice)) >= ?"
            params.append(min_price)
        
        if max_price:
            query += " AND (p.BasePrice + MIN(pv.AdditionalPrice)) <= ?"
            params.append(max_price)
        
        if in_stock_only:
            query += " AND SUM(pv.StockQuantity) > 0"
        
        try:
            cursor = self.conn.cursor()
            cursor.execute(query, params)
            
            columns = [column[0] for column in cursor.description]
            products = []
            
            for row in cursor.fetchall():
                product = dict(zip(columns, row))
                product['FinalPrice'] = product['BasePrice'] + (product['MinAdditionalPrice'] or 0)
                products.append(product)
            
            logger.info(f"✅ Found {len(products)} products matching filters")
            return products
            
        except Exception as e:
            logger.error(f"❌ Error searching products: {e}")
            return []
    
    def get_categories(self) -> List[Dict]:
        """Lấy danh sách tất cả categories"""
        query = """
        SELECT 
            CategoryID,
            CategoryName,
            ParentCategoryID
        FROM Categories
        ORDER BY CategoryName
        """
        
        try:
            cursor = self.conn.cursor()
            cursor.execute(query)
            
            columns = [column[0] for column in cursor.description]
            categories = [dict(zip(columns, row)) for row in cursor.fetchall()]
            
            logger.info(f"✅ Retrieved {len(categories)} categories")
            return categories
            
        except Exception as e:
            logger.error(f"❌ Error querying categories: {e}")
            return []


# ===== USAGE EXAMPLE =====
if __name__ == "__main__":
    # Test connection
    conn_string = "Driver={SQL Server};Server=DESKTOP-195HJGO\SQLEXPRESS;Database=OnlineJewelryStore;UID=sa;PWD=1;TrustServerCertificate=yes;"
    
    db = DatabaseConnector(conn_string)
    
    if db.connect():
        # Test queries
        products = db.get_all_products()
        print(f"Total products: {len(products)}")
        
        if products:
            print("\nSample product:")
            print(products[0])
        
        # Test search
        gold_products = db.search_products(metal_type='Gold', max_price=50000000)
        print(f"\nGold products under 50M: {len(gold_products)}")
        
        db.disconnect()