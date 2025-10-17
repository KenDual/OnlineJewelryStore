using OnlineJewelryStore.Filters;
using OnlineJewelryStore.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;

namespace OnlineJewelryStore.Areas.Admin.Controllers
{
    [AdminAuthorize]
    public class GemstonesController : Controller
    {
        private OnlineJewelryStoreEntities db = new OnlineJewelryStoreEntities();

        // GET: Admin/Gemstones/Index?variantId=123
        public ActionResult Index(int? variantId)
        {
            if (variantId == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            var variant = db.ProductVariants
                .Include(v => v.Product)
                .Include(v => v.Product.Category)
                .FirstOrDefault(v => v.VariantID == variantId);

            if (variant == null)
            {
                TempData["ErrorMessage"] = "Variant not found.";
                return RedirectToAction("Index", "Products");
            }

            // Load gemstones for this variant
            var gemstones = db.Gemstones
                .Where(g => g.VariantID == variantId)
                .OrderBy(g => g.GemstoneID)
                .ToList();

            // Pass variant info to view via ViewBag
            ViewBag.Variant = variant;
            ViewBag.Product = variant.Product;
            ViewBag.VariantID = variantId;

            return View(gemstones);
        }

        // GET: Admin/Gemstones/Create?variantId=123
        public ActionResult Create(int? variantId)
        {
            if (variantId == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            var variant = db.ProductVariants
                .Include(v => v.Product)
                .Include(v => v.Product.Category)
                .FirstOrDefault(v => v.VariantID == variantId);

            if (variant == null)
            {
                TempData["ErrorMessage"] = "Variant not found.";
                return RedirectToAction("Index", "Products");
            }

            // Pass variant info to view
            ViewBag.Variant = variant;
            ViewBag.Product = variant.Product;

            // Initialize model with VariantID
            var model = new Gemstone
            {
                VariantID = variantId.Value
            };

            // Populate dropdowns
            PopulateDropdowns();

            return View(model);
        }

        // POST: Admin/Gemstones/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(Gemstone gemstone)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    // Validate VariantID exists
                    var variant = db.ProductVariants.Find(gemstone.VariantID);
                    if (variant == null)
                    {
                        TempData["ErrorMessage"] = "Invalid Variant ID.";
                        return RedirectToAction("Index", "ProductVariants");
                    }

                    // Validate Carat if provided
                    if (gemstone.Carat.HasValue && gemstone.Carat.Value <= 0)
                    {
                        ModelState.AddModelError("Carat", "Carat must be greater than 0.");
                        PopulateDropdowns();
                        ViewBag.Variant = variant;
                        ViewBag.Product = variant.Product;
                        return View(gemstone);
                    }

                    db.Gemstones.Add(gemstone);
                    db.SaveChanges();

                    TempData["SuccessMessage"] = "Gemstone created successfully!";
                    return RedirectToAction("Index", new { variantId = gemstone.VariantID });
                }
                catch (Exception ex)
                {
                    TempData["ErrorMessage"] = "Error creating gemstone: " + ex.Message;
                }
            }

            // If we got this far, something failed, redisplay form
            var variantReload = db.ProductVariants
                .Include(v => v.Product)
                .FirstOrDefault(v => v.VariantID == gemstone.VariantID);

            ViewBag.Variant = variantReload;
            ViewBag.Product = variantReload?.Product;
            PopulateDropdowns();

            return View(gemstone);
        }

        // GET: Admin/Gemstones/Edit/5
        public ActionResult Edit(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            var gemstone = db.Gemstones
                .Include(g => g.ProductVariant)
                .Include(g => g.ProductVariant.Product)
                .Include(g => g.ProductVariant.Product.Category)
                .FirstOrDefault(g => g.GemstoneID == id);

            if (gemstone == null)
            {
                TempData["ErrorMessage"] = "Gemstone not found.";
                return RedirectToAction("Index", "Products");
            }

            // Pass variant and product info
            ViewBag.Variant = gemstone.ProductVariant;
            ViewBag.Product = gemstone.ProductVariant.Product;

            // Populate dropdowns
            PopulateDropdowns(gemstone);

            return View(gemstone);
        }

        // POST: Admin/Gemstones/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(Gemstone gemstone)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    // Validate Carat if provided
                    if (gemstone.Carat.HasValue && gemstone.Carat.Value <= 0)
                    {
                        ModelState.AddModelError("Carat", "Carat must be greater than 0.");

                        var variantError = db.ProductVariants
                            .Include(v => v.Product)
                            .FirstOrDefault(v => v.VariantID == gemstone.VariantID);

                        ViewBag.Variant = variantError;
                        ViewBag.Product = variantError?.Product;
                        PopulateDropdowns(gemstone);
                        return View(gemstone);
                    }

                    db.Entry(gemstone).State = EntityState.Modified;
                    db.SaveChanges();

                    TempData["SuccessMessage"] = "Gemstone updated successfully!";
                    return RedirectToAction("Index", new { variantId = gemstone.VariantID });
                }
                catch (Exception ex)
                {
                    TempData["ErrorMessage"] = "Error updating gemstone: " + ex.Message;
                }
            }

            // If we got this far, something failed, redisplay form
            var variant = db.ProductVariants
                .Include(v => v.Product)
                .FirstOrDefault(v => v.VariantID == gemstone.VariantID);

            ViewBag.Variant = variant;
            ViewBag.Product = variant?.Product;
            PopulateDropdowns(gemstone);

            return View(gemstone);
        }

        // GET: Admin/Gemstones/Delete/5
        public ActionResult Delete(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            var gemstone = db.Gemstones
                .Include(g => g.ProductVariant)
                .Include(g => g.ProductVariant.Product)
                .Include(g => g.ProductVariant.Product.Category)
                .FirstOrDefault(g => g.GemstoneID == id);

            if (gemstone == null)
            {
                TempData["ErrorMessage"] = "Gemstone not found.";
                return RedirectToAction("Index", "Products");
            }

            // Pass variant and product info
            ViewBag.Variant = gemstone.ProductVariant;
            ViewBag.Product = gemstone.ProductVariant.Product;

            return View(gemstone);
        }

        // POST: Admin/Gemstones/DeleteConfirmed
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            try
            {
                var gemstone = db.Gemstones.Find(id);

                if (gemstone == null)
                {
                    TempData["ErrorMessage"] = "Gemstone not found.";
                    return RedirectToAction("Index", "Products");
                }

                int variantId = gemstone.VariantID;

                // No safety checks needed - gemstones don't affect orders
                db.Gemstones.Remove(gemstone);
                db.SaveChanges();

                TempData["SuccessMessage"] = "Gemstone deleted successfully!";
                return RedirectToAction("Index", new { variantId = variantId });
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error deleting gemstone: " + ex.Message;
                return RedirectToAction("Delete", new { id = id });
            }
        }

        // Helper method to populate dropdowns
        private void PopulateDropdowns(Gemstone gemstone = null)
        {
            // Cut Types
            ViewBag.CutList = new SelectList(new[]
            {
                new { Value = "", Text = "-- Select Cut --" },
                new { Value = "Round", Text = "Round" },
                new { Value = "Princess", Text = "Princess" },
                new { Value = "Emerald", Text = "Emerald" },
                new { Value = "Cushion", Text = "Cushion" },
                new { Value = "Oval", Text = "Oval" },
                new { Value = "Pear", Text = "Pear" },
                new { Value = "Marquise", Text = "Marquise" },
                new { Value = "Heart", Text = "Heart" },
                new { Value = "Radiant", Text = "Radiant" },
                new { Value = "Asscher", Text = "Asscher" }
            }, "Value", "Text", gemstone?.Cut);

            // Clarity Grades
            ViewBag.ClarityList = new SelectList(new[]
            {
                new { Value = "", Text = "-- Select Clarity --" },
                new { Value = "FL", Text = "FL - Flawless" },
                new { Value = "IF", Text = "IF - Internally Flawless" },
                new { Value = "VVS1", Text = "VVS1 - Very Very Slightly Included 1" },
                new { Value = "VVS2", Text = "VVS2 - Very Very Slightly Included 2" },
                new { Value = "VS1", Text = "VS1 - Very Slightly Included 1" },
                new { Value = "VS2", Text = "VS2 - Very Slightly Included 2" },
                new { Value = "SI1", Text = "SI1 - Slightly Included 1" },
                new { Value = "SI2", Text = "SI2 - Slightly Included 2" },
                new { Value = "I1", Text = "I1 - Included 1" },
                new { Value = "I2", Text = "I2 - Included 2" },
                new { Value = "I3", Text = "I3 - Included 3" }
            }, "Value", "Text", gemstone?.Clarity);

            // Color Grades
            ViewBag.ColorList = new SelectList(new[]
            {
                new { Value = "", Text = "-- Select Color --" },
                new { Value = "D", Text = "D - Colorless" },
                new { Value = "E", Text = "E - Colorless" },
                new { Value = "F", Text = "F - Colorless" },
                new { Value = "G", Text = "G - Near Colorless" },
                new { Value = "H", Text = "H - Near Colorless" },
                new { Value = "I", Text = "I - Near Colorless" },
                new { Value = "J", Text = "J - Near Colorless" },
                new { Value = "K", Text = "K - Faint Yellow" },
                new { Value = "L", Text = "L - Faint Yellow" },
                new { Value = "M", Text = "M - Faint Yellow" },
                new { Value = "N", Text = "N - Very Light Yellow" }
            }, "Value", "Text", gemstone?.Color);

            // Treatment Types
            ViewBag.TreatmentList = new SelectList(new[]
            {
                new { Value = "", Text = "-- Select Treatment --" },
                new { Value = "None", Text = "None" },
                new { Value = "Heat", Text = "Heat" },
                new { Value = "Irradiation", Text = "Irradiation" },
                new { Value = "HPHT", Text = "HPHT (High Pressure High Temperature)" },
                new { Value = "Fracture Filling", Text = "Fracture Filling" },
                new { Value = "Laser Drilling", Text = "Laser Drilling" },
                new { Value = "Coating", Text = "Coating" }
            }, "Value", "Text", gemstone?.Treatment);
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
