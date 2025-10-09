using OnlineJewelryStore.Filters;
using OnlineJewelryStore.Models;
using System;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;

namespace OnlineJewelryStore.Areas.Admin.Controllers
{
    [AdminAuthorize]
    public class ProductMediasController : Controller
    {
        private OnlineJewelryStoreEntities db = new OnlineJewelryStoreEntities();

        // GET: Admin/ProductMedia/Index/5
        public ActionResult Index(int? productId)
        {
            ViewBag.ActiveMenu = "Products";

            if (productId == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            var product = db.Products
                .Include(p => p.Category)
                .Include(p => p.ProductMedias)
                .FirstOrDefault(p => p.ProductID == productId);

            if (product == null)
            {
                return HttpNotFound();
            }

            ViewBag.Product = product;
            return View(product.ProductMedias.ToList());
        }

        // GET: Admin/ProductMedia/Upload/5
        public ActionResult Upload(int? productId)
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
            ViewBag.MediaTypes = new SelectList(new[] { "Image", "Video", "360View" });

            var media = new ProductMedia
            {
                ProductID = productId.Value,
                IsMain = false,
                MediaType = "Image"
            };

            return View(media);
        }

        // POST: Admin/ProductMedia/Upload
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Upload(ProductMedia media, HttpPostedFileBase file)
        {
            if (file != null && file.ContentLength > 0)
            {
                // Validate file type
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".mp4", ".mov", ".avi" };
                var extension = Path.GetExtension(file.FileName).ToLower();

                if (!allowedExtensions.Contains(extension))
                {
                    ModelState.AddModelError("file", "Invalid file type. Allowed: JPG, PNG, GIF, MP4, MOV, AVI");
                    ViewBag.Product = db.Products.Find(media.ProductID);
                    ViewBag.MediaTypes = new SelectList(new[] { "Image", "Video", "360View" }, media.MediaType);
                    return View(media);
                }

                // Validate file size (max 10MB)
                if (file.ContentLength > 10 * 1024 * 1024)
                {
                    ModelState.AddModelError("file", "File size must be less than 10MB");
                    ViewBag.Product = db.Products.Find(media.ProductID);
                    ViewBag.MediaTypes = new SelectList(new[] { "Image", "Video", "360View" }, media.MediaType);
                    return View(media);
                }

                try
                {
                    // Create directory if not exists
                    var uploadDir = Server.MapPath($"~/Content/uploads/products/{media.ProductID}");
                    if (!Directory.Exists(uploadDir))
                    {
                        Directory.CreateDirectory(uploadDir);
                    }

                    // Generate unique filename
                    var fileName = $"{Guid.NewGuid()}{extension}";
                    var filePath = Path.Combine(uploadDir, fileName);

                    // Save file
                    file.SaveAs(filePath);

                    // Save to database
                    media.URL = $"/Content/uploads/products/{media.ProductID}/{fileName}";

                    // If this is the first media, set as main
                    if (!db.ProductMedias.Any(m => m.ProductID == media.ProductID))
                    {
                        media.IsMain = true;
                    }

                    // If user set as main, unset other main images
                    if (media.IsMain)
                    {
                        var existingMain = db.ProductMedias
                            .Where(m => m.ProductID == media.ProductID && m.IsMain)
                            .ToList();

                        foreach (var m in existingMain)
                        {
                            m.IsMain = false;
                        }
                    }

                    db.ProductMedias.Add(media);
                    db.SaveChanges();

                    TempData["SuccessMessage"] = "Media uploaded successfully!";
                    return RedirectToAction("Index", new { productId = media.ProductID });
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", $"Error uploading file: {ex.Message}");
                }
            }
            else
            {
                ModelState.AddModelError("file", "Please select a file to upload");
            }

            ViewBag.Product = db.Products.Find(media.ProductID);
            ViewBag.MediaTypes = new SelectList(new[] { "Image", "Video", "360View" }, media.MediaType);
            return View(media);
        }

        // POST: Admin/ProductMedia/SetMain/5
        [HttpPost]
        public ActionResult SetMain(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            var media = db.ProductMedias.Find(id);
            if (media == null)
            {
                return HttpNotFound();
            }

            // Unset all other main images for this product
            var otherMainImages = db.ProductMedias
                .Where(m => m.ProductID == media.ProductID && m.MediaID != id && m.IsMain)
                .ToList();

            foreach (var m in otherMainImages)
            {
                m.IsMain = false;
            }

            // Set this as main
            media.IsMain = true;
            db.SaveChanges();

            TempData["SuccessMessage"] = "Main image updated successfully!";
            return RedirectToAction("Index", new { productId = media.ProductID });
        }

        // GET: Admin/ProductMedia/Delete/5
        public ActionResult Delete(int? id)
        {
            ViewBag.ActiveMenu = "Products";

            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            var media = db.ProductMedias
                .Include(m => m.Product)
                .FirstOrDefault(m => m.MediaID == id);

            if (media == null)
            {
                return HttpNotFound();
            }

            ViewBag.Product = media.Product;
            return View(media);
        }

        // POST: Admin/ProductMedia/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            var media = db.ProductMedias.Find(id);
            if (media == null)
            {
                return HttpNotFound();
            }

            var productId = media.ProductID;

            try
            {
                // Delete physical file
                var filePath = Server.MapPath(media.URL);
                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                }

                // If this was main image, set another one as main
                if (media.IsMain)
                {
                    var firstMedia = db.ProductMedias
                        .Where(m => m.ProductID == productId && m.MediaID != id)
                        .FirstOrDefault();

                    if (firstMedia != null)
                    {
                        firstMedia.IsMain = true;
                    }
                }

                // Delete from database
                db.ProductMedias.Remove(media);
                db.SaveChanges();

                TempData["SuccessMessage"] = "Media deleted successfully!";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error deleting media: {ex.Message}";
            }

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