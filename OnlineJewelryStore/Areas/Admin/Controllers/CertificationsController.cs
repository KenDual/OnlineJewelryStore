using OnlineJewelryStore.Filters;
using OnlineJewelryStore.Models;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;

namespace OnlineJewelryStore.Areas.Admin.Controllers
{
    [AdminAuthorize]
    public class CertificationsController : Controller
    {
        private OnlineJewelryStoreEntities db = new OnlineJewelryStoreEntities();

        public ActionResult Index(int? variantId)
        {
            if (variantId == null)
            {
                TempData["ErrorMessage"] = "Variant ID is required.";
                return RedirectToAction("Index", "ProductVariants");
            }

            // Lấy thông tin Variant với Product và Category
            var variant = db.ProductVariants
                .Include(v => v.Product.Category)
                .FirstOrDefault(v => v.VariantID == variantId.Value);

            if (variant == null)
            {
                TempData["ErrorMessage"] = "Product Variant not found.";
                return RedirectToAction("Index", "ProductVariants");
            }

            // Lấy danh sách certifications của variant này
            var certifications = db.Certifications
                .Where(c => c.VariantID == variantId.Value)
                .OrderByDescending(c => c.CertificationID)
                .ToList();

            // Truyền thông tin variant qua ViewBag để hiển thị context
            ViewBag.VariantID = variantId.Value;
            ViewBag.ProductID = variant.ProductID;
            ViewBag.ProductName = variant.Product.ProductName;
            ViewBag.CategoryName = variant.Product.Category.CategoryName;
            ViewBag.SKU = variant.SKU;
            ViewBag.MetalType = variant.MetalType;
            ViewBag.Purity = variant.Purity;
            ViewBag.StockQuantity = variant.StockQuantity;

            return View(certifications);
        }

        // ==================== CREATE - ADD NEW CERTIFICATION ====================
        /// <summary>
        /// GET: Certifications/Create/5
        /// Hiển thị form tạo certification mới
        /// </summary>
        /// <param name="variantId">ID của ProductVariant</param>
        public ActionResult Create(int? variantId)
        {
            if (variantId == null)
            {
                TempData["ErrorMessage"] = "Variant ID is required.";
                return RedirectToAction("Index", "ProductVariants");
            }

            // Kiểm tra variant có tồn tại không
            var variant = db.ProductVariants
                .Include(v => v.Product.Category)
                .FirstOrDefault(v => v.VariantID == variantId.Value);

            if (variant == null)
            {
                TempData["ErrorMessage"] = "Product Variant not found.";
                return RedirectToAction("Index", "ProductVariants");
            }

            // Tạo model mới với VariantID
            var certification = new Certification
            {
                VariantID = variantId.Value
            };

            // Truyền thông tin variant qua ViewBag
            ViewBag.VariantID = variantId.Value;
            ViewBag.ProductID = variant.ProductID;
            ViewBag.ProductName = variant.Product.ProductName;
            ViewBag.CategoryName = variant.Product.Category.CategoryName;
            ViewBag.SKU = variant.SKU;
            ViewBag.MetalType = variant.MetalType;
            ViewBag.Purity = variant.Purity;

            // Dropdown cho Certifier
            ViewBag.CertifierList = new SelectList(new[]
            {
                new { Value = "GIA", Text = "GIA (Gemological Institute of America)" },
                new { Value = "AGS", Text = "AGS (American Gem Society)" },
                new { Value = "Other", Text = "Other Certifier" }
            }, "Value", "Text");

            // Breadcrumb
            ViewBag.Breadcrumb = new[]
            {
                new { Title = "Products", Url = Url.Action("Index", "Products") },
                new { Title = variant.Product.ProductName, Url = Url.Action("Details", "Products", new { id = variant.ProductID }) },
                new { Title = "Variants", Url = Url.Action("Index", "ProductVariants", new { productId = variant.ProductID }) },
                new { Title = $"SKU: {variant.SKU}", Url = Url.Action("Index", "Certifications", new { variantId = variantId.Value }) },
                new { Title = "Add Certification", Url = (string)null }
            };

            return View(certification);
        }

        // POST: Certifications/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(Certification certification)
        {
            // Kiểm tra variant có tồn tại không
            var variant = db.ProductVariants
                .Include(v => v.Product.Category)
                .FirstOrDefault(v => v.VariantID == certification.VariantID);

            if (variant == null)
            {
                TempData["ErrorMessage"] = "Product Variant not found.";
                return RedirectToAction("Index", "ProductVariants");
            }

            // ========== VALIDATIONS ==========

            // 1. Validate Certifier (GIA, AGS, Other)
            if (string.IsNullOrWhiteSpace(certification.Certifier))
            {
                ModelState.AddModelError("Certifier", "Certifier is required.");
            }
            else if (!new[] { "GIA", "AGS", "Other" }.Contains(certification.Certifier))
            {
                ModelState.AddModelError("Certifier", "Certifier must be GIA, AGS, or Other.");
            }

            // 2. Validate CertificateNumber - Must be UNIQUE if provided
            if (!string.IsNullOrWhiteSpace(certification.CertificateNumber))
            {
                // Trim whitespace
                certification.CertificateNumber = certification.CertificateNumber.Trim();

                // Check duplicate
                var isDuplicate = db.Certifications
                    .Any(c => c.CertificateNumber == certification.CertificateNumber);

                if (isDuplicate)
                {
                    ModelState.AddModelError("CertificateNumber",
                        $"Certificate Number '{certification.CertificateNumber}' already exists. It must be unique.");
                }
            }
            else
            {
                // Set to null nếu empty
                certification.CertificateNumber = null;
            }

            // 3. Validate VerificationURL - Optional but must be valid URL format
            if (!string.IsNullOrWhiteSpace(certification.VerificationURL))
            {
                certification.VerificationURL = certification.VerificationURL.Trim();

                // Check URL format
                if (!Uri.TryCreate(certification.VerificationURL, UriKind.Absolute, out Uri uriResult) ||
                    (uriResult.Scheme != Uri.UriSchemeHttp && uriResult.Scheme != Uri.UriSchemeHttps))
                {
                    ModelState.AddModelError("VerificationURL",
                        "Verification URL must be a valid HTTP or HTTPS URL (e.g., https://example.com).");
                }

                // Check max length
                if (certification.VerificationURL.Length > 255)
                {
                    ModelState.AddModelError("VerificationURL",
                        "Verification URL cannot exceed 255 characters.");
                }
            }
            else
            {
                certification.VerificationURL = null;
            }

            // ========== SAVE TO DATABASE ==========
            if (ModelState.IsValid)
            {
                try
                {
                    db.Certifications.Add(certification);
                    db.SaveChanges();

                    TempData["SuccessMessage"] = $"Certification from {certification.Certifier} has been added successfully!";
                    return RedirectToAction("Index", new { variantId = certification.VariantID });
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", $"Error saving certification: {ex.Message}");
                }
            }

            // ========== RETURN VIEW WITH ERRORS ==========
            // Repopulate ViewBag
            ViewBag.VariantID = certification.VariantID;
            ViewBag.ProductID = variant.ProductID;
            ViewBag.ProductName = variant.Product.ProductName;
            ViewBag.CategoryName = variant.Product.Category.CategoryName;
            ViewBag.SKU = variant.SKU;
            ViewBag.MetalType = variant.MetalType;
            ViewBag.Purity = variant.Purity;

            ViewBag.CertifierList = new SelectList(new[]
            {
                new { Value = "GIA", Text = "GIA (Gemological Institute of America)" },
                new { Value = "AGS", Text = "AGS (American Gem Society)" },
                new { Value = "Other", Text = "Other Certifier" }
            }, "Value", "Text", certification.Certifier);

            ViewBag.Breadcrumb = new[]
            {
                new { Title = "Products", Url = Url.Action("Index", "Products") },
                new { Title = variant.Product.ProductName, Url = Url.Action("Details", "Products", new { id = variant.ProductID }) },
                new { Title = "Variants", Url = Url.Action("Index", "ProductVariants", new { productId = variant.ProductID }) },
                new { Title = $"SKU: {variant.SKU}", Url = Url.Action("Index", "Certifications", new { variantId = certification.VariantID }) },
                new { Title = "Add Certification", Url = (string)null }
            };

            return View(certification);
        }

        // GET: Certifications/Edit/5
        public ActionResult Edit(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            // Lấy certification với variant, product, category
            var certification = db.Certifications
                .Include(c => c.ProductVariant.Product.Category)
                .FirstOrDefault(c => c.CertificationID == id.Value);

            if (certification == null)
            {
                TempData["ErrorMessage"] = "Certification not found.";
                return RedirectToAction("Index", "ProductVariants");
            }

            var variant = certification.ProductVariant;

            // Truyền thông tin qua ViewBag
            ViewBag.CertificationID = certification.CertificationID;
            ViewBag.VariantID = variant.VariantID;
            ViewBag.ProductID = variant.ProductID;
            ViewBag.ProductName = variant.Product.ProductName;
            ViewBag.CategoryName = variant.Product.Category.CategoryName;
            ViewBag.SKU = variant.SKU;
            ViewBag.MetalType = variant.MetalType;
            ViewBag.Purity = variant.Purity;

            // Dropdown cho Certifier
            ViewBag.CertifierList = new SelectList(new[]
            {
                new { Value = "GIA", Text = "GIA (Gemological Institute of America)" },
                new { Value = "AGS", Text = "AGS (American Gem Society)" },
                new { Value = "Other", Text = "Other Certifier" }
            }, "Value", "Text", certification.Certifier);

            // Breadcrumb
            ViewBag.Breadcrumb = new[]
            {
                new { Title = "Products", Url = Url.Action("Index", "Products") },
                new { Title = variant.Product.ProductName, Url = Url.Action("Details", "Products", new { id = variant.ProductID }) },
                new { Title = "Variants", Url = Url.Action("Index", "ProductVariants", new { productId = variant.ProductID }) },
                new { Title = $"SKU: {variant.SKU}", Url = Url.Action("Index", "Certifications", new { variantId = variant.VariantID }) },
                new { Title = "Edit Certification", Url = (string)null }
            };

            return View(certification);
        }

        // POST: Certifications/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(Certification certification)
        {
            // Kiểm tra certification có tồn tại không
            var existingCert = db.Certifications
                .Include(c => c.ProductVariant.Product.Category)
                .FirstOrDefault(c => c.CertificationID == certification.CertificationID);

            if (existingCert == null)
            {
                TempData["ErrorMessage"] = "Certification not found.";
                return RedirectToAction("Index", "ProductVariants");
            }

            var variant = existingCert.ProductVariant;

            // ========== VALIDATIONS ==========

            // 1. Validate Certifier
            if (string.IsNullOrWhiteSpace(certification.Certifier))
            {
                ModelState.AddModelError("Certifier", "Certifier is required.");
            }
            else if (!new[] { "GIA", "AGS", "Other" }.Contains(certification.Certifier))
            {
                ModelState.AddModelError("Certifier", "Certifier must be GIA, AGS, or Other.");
            }

            // 2. Validate CertificateNumber - Must be UNIQUE (except current record)
            if (!string.IsNullOrWhiteSpace(certification.CertificateNumber))
            {
                certification.CertificateNumber = certification.CertificateNumber.Trim();

                // Check duplicate (exclude current certification)
                var isDuplicate = db.Certifications
                    .Any(c => c.CertificateNumber == certification.CertificateNumber
                          && c.CertificationID != certification.CertificationID);

                if (isDuplicate)
                {
                    ModelState.AddModelError("CertificateNumber",
                        $"Certificate Number '{certification.CertificateNumber}' already exists. It must be unique.");
                }
            }
            else
            {
                certification.CertificateNumber = null;
            }

            // 3. Validate VerificationURL
            if (!string.IsNullOrWhiteSpace(certification.VerificationURL))
            {
                certification.VerificationURL = certification.VerificationURL.Trim();

                if (!Uri.TryCreate(certification.VerificationURL, UriKind.Absolute, out Uri uriResult) ||
                    (uriResult.Scheme != Uri.UriSchemeHttp && uriResult.Scheme != Uri.UriSchemeHttps))
                {
                    ModelState.AddModelError("VerificationURL",
                        "Verification URL must be a valid HTTP or HTTPS URL.");
                }

                if (certification.VerificationURL.Length > 255)
                {
                    ModelState.AddModelError("VerificationURL",
                        "Verification URL cannot exceed 255 characters.");
                }
            }
            else
            {
                certification.VerificationURL = null;
            }

            // ========== UPDATE DATABASE ==========
            if (ModelState.IsValid)
            {
                try
                {
                    // Update fields
                    existingCert.Certifier = certification.Certifier;
                    existingCert.CertificateNumber = certification.CertificateNumber;
                    existingCert.VerificationURL = certification.VerificationURL;

                    db.Entry(existingCert).State = EntityState.Modified;
                    db.SaveChanges();

                    TempData["SuccessMessage"] = $"Certification from {certification.Certifier} has been updated successfully!";
                    return RedirectToAction("Index", new { variantId = existingCert.VariantID });
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", $"Error updating certification: {ex.Message}");
                }
            }

            // ========== RETURN VIEW WITH ERRORS ==========
            ViewBag.CertificationID = certification.CertificationID;
            ViewBag.VariantID = variant.VariantID;
            ViewBag.ProductID = variant.ProductID;
            ViewBag.ProductName = variant.Product.ProductName;
            ViewBag.CategoryName = variant.Product.Category.CategoryName;
            ViewBag.SKU = variant.SKU;
            ViewBag.MetalType = variant.MetalType;
            ViewBag.Purity = variant.Purity;

            ViewBag.CertifierList = new SelectList(new[]
            {
                new { Value = "GIA", Text = "GIA (Gemological Institute of America)" },
                new { Value = "AGS", Text = "AGS (American Gem Society)" },
                new { Value = "Other", Text = "Other Certifier" }
            }, "Value", "Text", certification.Certifier);

            ViewBag.Breadcrumb = new[]
            {
                new { Title = "Products", Url = Url.Action("Index", "Products") },
                new { Title = variant.Product.ProductName, Url = Url.Action("Details", "Products", new { id = variant.ProductID }) },
                new { Title = "Variants", Url = Url.Action("Index", "ProductVariants", new { productId = variant.ProductID }) },
                new { Title = $"SKU: {variant.SKU}", Url = Url.Action("Index", "Certifications", new { variantId = variant.VariantID }) },
                new { Title = "Edit Certification", Url = (string)null }
            };

            return View(certification);
        }

        // GET: Certifications/Delete/5
        public ActionResult Delete(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            // Lấy certification với variant, product, category
            var certification = db.Certifications
                .Include(c => c.ProductVariant.Product.Category)
                .FirstOrDefault(c => c.CertificationID == id.Value);

            if (certification == null)
            {
                TempData["ErrorMessage"] = "Certification not found.";
                return RedirectToAction("Index", "ProductVariants");
            }

            var variant = certification.ProductVariant;

            // Truyền thông tin qua ViewBag
            ViewBag.CertificationID = certification.CertificationID;
            ViewBag.VariantID = variant.VariantID;
            ViewBag.ProductID = variant.ProductID;
            ViewBag.ProductName = variant.Product.ProductName;
            ViewBag.CategoryName = variant.Product.Category.CategoryName;
            ViewBag.SKU = variant.SKU;
            ViewBag.MetalType = variant.MetalType;
            ViewBag.Purity = variant.Purity;
            ViewBag.StockQuantity = variant.StockQuantity;

            // Breadcrumb
            ViewBag.Breadcrumb = new[]
            {
                new { Title = "Products", Url = Url.Action("Index", "Products") },
                new { Title = variant.Product.ProductName, Url = Url.Action("Details", "Products", new { id = variant.ProductID }) },
                new { Title = "Variants", Url = Url.Action("Index", "ProductVariants", new { productId = variant.ProductID }) },
                new { Title = $"SKU: {variant.SKU}", Url = Url.Action("Index", "Certifications", new { variantId = variant.VariantID }) },
                new { Title = "Delete Certification", Url = (string)null }
            };

            return View(certification);
        }

        // POST: Certifications/DeleteConfirmed
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            try
            {
                var certification = db.Certifications
                    .FirstOrDefault(c => c.CertificationID == id);

                if (certification == null)
                {
                    TempData["ErrorMessage"] = "Certification not found.";
                    return RedirectToAction("Index", "ProductVariants");
                }

                int variantId = certification.VariantID;
                string certifier = certification.Certifier;
                string certNumber = certification.CertificateNumber ?? "N/A";

                // Xóa certification (không cần safety check vì không ảnh hưởng orders)
                db.Certifications.Remove(certification);
                db.SaveChanges();

                TempData["SuccessMessage"] = $"Certification from {certifier} (Certificate: {certNumber}) has been deleted successfully!";
                return RedirectToAction("Index", new { variantId = variantId });
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error deleting certification: {ex.Message}";
                return RedirectToAction("Delete", new { id = id });
            }
        }

        // ==================== DISPOSE ====================
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