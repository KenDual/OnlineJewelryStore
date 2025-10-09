using OnlineJewelryStore.Filters;
using OnlineJewelryStore.Models;
using System;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web.Mvc;

namespace OnlineJewelryStore.Areas.Admin.Controllers
{
    [AdminAuthorize]
    public class ProductVariantsController : Controller
    {
        private OnlineJewelryStoreEntities db = new OnlineJewelryStoreEntities();

        // GET: Admin/ProductVariants/Index/5
        public ActionResult Index(int? productId)
        {
            ViewBag.ActiveMenu = "Products";

            if (productId == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            var product = db.Products
                .Include(p => p.Category)
                .Include(p => p.ProductVariants)
                .FirstOrDefault(p => p.ProductID == productId);

            if (product == null)
            {
                return HttpNotFound();
            }

            ViewBag.Product = product;
            return View(product.ProductVariants.ToList());
        }

        // GET: Admin/ProductVariants/Create/5
        public ActionResult Create(int? productId)
        {
            ViewBag.ActiveMenu = "Products";

            if (productId == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            var product = db.Products.Find(productId);
            if (product == null)
            {
                return HttpNotFound();
            }

            ViewBag.Product = product;

            // Populate dropdown lists
            ViewBag.MetalTypes = new SelectList(new[] { "Gold", "Platinum", "Silver", "Rose Gold" });
            ViewBag.Purities = new SelectList(new[] { "14K", "18K", "24K", "925", "950", "999" });

            var variant = new ProductVariant
            {
                ProductID = productId.Value,
                AdditionalPrice = 0,
                StockQuantity = 0
            };

            return View(variant);
        }

        // POST: Admin/ProductVariants/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create([Bind(Include = "ProductID,SKU,MetalType,Purity,RingSize,ChainLength,StockQuantity,AdditionalPrice")] ProductVariant variant)
        {
            if (ModelState.IsValid)
            {
                // Check SKU uniqueness
                if (db.ProductVariants.Any(v => v.SKU == variant.SKU))
                {
                    ModelState.AddModelError("SKU", "This SKU already exists. Please use a unique SKU.");
                    ViewBag.Product = db.Products.Find(variant.ProductID);
                    ViewBag.MetalTypes = new SelectList(new[] { "Gold", "Platinum", "Silver", "Rose Gold" }, variant.MetalType);
                    ViewBag.Purities = new SelectList(new[] { "14K", "18K", "24K", "925", "950", "999" }, variant.Purity);
                    return View(variant);
                }

                variant.CreatedAt = DateTime.Now;
                db.ProductVariants.Add(variant);
                db.SaveChanges();

                TempData["SuccessMessage"] = $"Variant '{variant.SKU}' has been created successfully!";
                return RedirectToAction("Index", new { productId = variant.ProductID });
            }

            ViewBag.Product = db.Products.Find(variant.ProductID);
            ViewBag.MetalTypes = new SelectList(new[] { "Gold", "Platinum", "Silver", "Rose Gold" }, variant.MetalType);
            ViewBag.Purities = new SelectList(new[] { "14K", "18K", "24K", "925", "950", "999" }, variant.Purity);
            return View(variant);
        }

        // GET: Admin/ProductVariants/Edit/5
        public ActionResult Edit(int? id)
        {
            ViewBag.ActiveMenu = "Products";

            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            var variant = db.ProductVariants
                .Include(v => v.Product)
                .FirstOrDefault(v => v.VariantID == id);

            if (variant == null)
            {
                return HttpNotFound();
            }

            ViewBag.Product = variant.Product;
            ViewBag.MetalTypes = new SelectList(new[] { "Gold", "Platinum", "Silver", "Rose Gold" }, variant.MetalType);
            ViewBag.Purities = new SelectList(new[] { "14K", "18K", "24K", "925", "950", "999" }, variant.Purity);

            return View(variant);
        }

        // POST: Admin/ProductVariants/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit([Bind(Include = "VariantID,ProductID,SKU,MetalType,Purity,RingSize,ChainLength,StockQuantity,AdditionalPrice,CreatedAt")] ProductVariant variant)
        {
            if (ModelState.IsValid)
            {
                // Check SKU uniqueness (excluding current variant)
                if (db.ProductVariants.Any(v => v.SKU == variant.SKU && v.VariantID != variant.VariantID))
                {
                    ModelState.AddModelError("SKU", "This SKU already exists. Please use a unique SKU.");
                    ViewBag.Product = db.Products.Find(variant.ProductID);
                    ViewBag.MetalTypes = new SelectList(new[] { "Gold", "Platinum", "Silver", "Rose Gold" }, variant.MetalType);
                    ViewBag.Purities = new SelectList(new[] { "14K", "18K", "24K", "925", "950", "999" }, variant.Purity);
                    return View(variant);
                }

                db.Entry(variant).State = EntityState.Modified;
                db.SaveChanges();

                TempData["SuccessMessage"] = $"Variant '{variant.SKU}' has been updated successfully!";
                return RedirectToAction("Index", new { productId = variant.ProductID });
            }

            ViewBag.Product = db.Products.Find(variant.ProductID);
            ViewBag.MetalTypes = new SelectList(new[] { "Gold", "Platinum", "Silver", "Rose Gold" }, variant.MetalType);
            ViewBag.Purities = new SelectList(new[] { "14K", "18K", "24K", "925", "950", "999" }, variant.Purity);
            return View(variant);
        }

        // GET: Admin/ProductVariants/Delete/5
        public ActionResult Delete(int? id)
        {
            ViewBag.ActiveMenu = "Products";

            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            var variant = db.ProductVariants
                .Include(v => v.Product)
                .Include(v => v.OrderItems)
                .Include(v => v.CartItems)
                .Include(v => v.Wishlists)
                .FirstOrDefault(v => v.VariantID == id);

            if (variant == null)
            {
                return HttpNotFound();
            }

            ViewBag.Product = variant.Product;
            return View(variant);
        }

        // POST: Admin/ProductVariants/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            var variant = db.ProductVariants
                .Include(v => v.OrderItems)
                .FirstOrDefault(v => v.VariantID == id);

            if (variant == null)
            {
                return HttpNotFound();
            }

            var productId = variant.ProductID;

            // Safety check: Cannot delete if in orders
            if (variant.OrderItems != null && variant.OrderItems.Any())
            {
                TempData["ErrorMessage"] = $"Cannot delete variant '{variant.SKU}' because it has {variant.OrderItems.Count()} order(s).";
                return RedirectToAction("Index", new { productId = productId });
            }

            // Remove from carts and wishlists first
            var cartItems = variant.CartItems?.ToList();
            if (cartItems != null && cartItems.Any())
            {
                db.CartItems.RemoveRange(cartItems);
            }

            var wishlists = variant.Wishlists?.ToList();
            if (wishlists != null && wishlists.Any())
            {
                db.Wishlists.RemoveRange(wishlists);
            }

            db.ProductVariants.Remove(variant);
            db.SaveChanges();

            TempData["SuccessMessage"] = $"Variant '{variant.SKU}' has been deleted successfully!";
            return RedirectToAction("Index", new { productId = productId });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}