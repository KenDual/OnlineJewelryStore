using Newtonsoft.Json;
using OnlineJewelryStore.Models;
using OnlineJewelryStore.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;

namespace OnlineJewelryStore.Controllers
{
    public class ChatbotController : Controller
    {
        private readonly OnlineJewelryStoreEntities db = new OnlineJewelryStoreEntities();

        // AI Service configuration
        private static readonly string AI_SERVICE_URL = System.Configuration.ConfigurationManager.AppSettings["AIServiceUrl"];
        private const int REQUEST_TIMEOUT_SECONDS = 180;  // ✅ 3 phút cho lần đầu load model

        // HttpClient singleton với proper configuration
        private static readonly HttpClient httpClient = new HttpClient
        {
            BaseAddress = new Uri(AI_SERVICE_URL),
            Timeout = TimeSpan.FromSeconds(REQUEST_TIMEOUT_SECONDS)
        };

        // Static constructor để setup HttpClient một lần
        static ChatbotController()
        {
            httpClient.DefaultRequestHeaders.Accept.Clear();
            httpClient.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json")
            );
        }

        // POST: /Chatbot/Chat
        // Main endpoint để gửi message đến AI service
        [HttpPost]
        public async Task<JsonResult> Chat(ChatRequestViewModel model)
        {
            try
            {
                // Validate model
                if (!ModelState.IsValid)
                {
                    return Json(new ChatResponseViewModel
                    {
                        Success = false,
                        Error = "Invalid request. Please check your input.",
                        Message = "Sorry, request is invalid. Please try again."
                    });
                }

                // ✅ Prepare request payload - FIXED version
                // Xử lý conversation history trước
                var conversationHistory = model.ConversationHistory?
                    .Select(m => new
                    {
                        role = m.Role,
                        content = m.Content
                    })
                    .ToList();

                // Tạo request payload
                var requestPayload = new
                {
                    message = model.Message?.Trim() ?? "",
                    category = string.IsNullOrWhiteSpace(model.Category) ? (string)null : model.Category,
                    min_price = model.MinPrice.HasValue ? (object)model.MinPrice.Value : null,
                    max_price = model.MaxPrice.HasValue ? (object)model.MaxPrice.Value : null,
                    conversation_history = conversationHistory
                };

                // Serialize to JSON
                var jsonContent = JsonConvert.SerializeObject(requestPayload,
                    new JsonSerializerSettings
                    {
                        NullValueHandling = NullValueHandling.Include
                    });

                var httpContent = new StringContent(
                    jsonContent,
                    Encoding.UTF8,
                    "application/json"
                );

                // ✅ LOG REQUEST (optional - for debugging)
                System.Diagnostics.Debug.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] AI Request: {jsonContent}");

                // Call AI service
                var response = await httpClient.PostAsync("/chat", httpContent);

                // Read response
                var responseContent = await response.Content.ReadAsStringAsync();

                // ✅ LOG RESPONSE (optional - for debugging)
                System.Diagnostics.Debug.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] AI Response [{response.StatusCode}]: {responseContent.Substring(0, Math.Min(200, responseContent.Length))}...");

                if (response.IsSuccessStatusCode)
                {
                    // Parse response
                    var aiResponse = JsonConvert.DeserializeObject<dynamic>(responseContent);

                    // Convert to ViewModel
                    var chatResponse = new ChatResponseViewModel
                    {
                        Success = true,
                        Message = aiResponse.message,
                        Timestamp = aiResponse.timestamp,
                        Products = new List<ProductSuggestionViewModel>()
                    };

                    // Map products
                    if (aiResponse.products != null)
                    {
                        foreach (var product in aiResponse.products)
                        {
                            chatResponse.Products.Add(new ProductSuggestionViewModel
                            {
                                Id = product.id,
                                Name = product.name,
                                Price = product.price,
                                Category = product.category,
                                Image = product.image,
                                Score = product.score,
                                ProductUrl = Url.Action("Details", "Shop", new { id = (int)product.id })
                            });
                        }
                    }

                    return Json(chatResponse);
                }
                else
                {
                    // ✅ AI service error - LOG CHI TIẾT
                    var errorDetail = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] AI Service Error: {response.StatusCode}\n" +
                                     $"Request: {jsonContent}\n" +
                                     $"Response: {responseContent}\n";

                    System.Diagnostics.Debug.WriteLine(errorDetail);

                    // Log to file
                    try
                    {
                        var logPath = Server.MapPath("~/App_Data/chatbot_errors.log");
                        System.IO.File.AppendAllText(logPath, errorDetail + "\n");
                    }
                    catch { /* Ignore logging errors */ }

                    // Parse error message từ FastAPI nếu có
                    string userMessage = "Xin lỗi, hệ thống AI đang gặp sự cố. Vui lòng thử lại.";
                    try
                    {
                        var errorJson = JsonConvert.DeserializeObject<dynamic>(responseContent);
                        if (errorJson?.detail != null)
                        {
                            userMessage = $"Lỗi: {errorJson.detail}";
                        }
                    }
                    catch { /* Use default message */ }

                    return Json(new ChatResponseViewModel
                    {
                        Success = false,
                        Error = $"AI service error: {response.StatusCode}",
                        Message = userMessage
                    });
                }
            }
            catch (TaskCanceledException ex)
            {
                // ✅ Timeout - LOG CHI TIẾT
                var timeoutDetail = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] TIMEOUT after {REQUEST_TIMEOUT_SECONDS}s\n" +
                                   $"Exception: {ex.Message}\n";

                System.Diagnostics.Debug.WriteLine(timeoutDetail);

                // Log to file
                try
                {
                    var logPath = Server.MapPath("~/App_Data/chatbot_errors.log");
                    System.IO.File.AppendAllText(logPath, timeoutDetail + "\n");
                }
                catch { /* Ignore logging errors */ }

                return Json(new ChatResponseViewModel
                {
                    Success = false,
                    Error = "Request timeout",
                    Message = $"Yêu cầu mất quá nhiều thời gian (>{REQUEST_TIMEOUT_SECONDS}s). " +
                             "Lần đầu có thể cần 1-2 phút để load model. Vui lòng thử lại."
                });
            }
            catch (HttpRequestException ex)
            {
                // ✅ Connection error - LOG CHI TIẾT
                var connError = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] CONNECTION ERROR\n" +
                               $"Exception: {ex.Message}\n" +
                               $"AI Service URL: {AI_SERVICE_URL}\n";

                System.Diagnostics.Debug.WriteLine(connError);

                // Log to file
                try
                {
                    var logPath = Server.MapPath("~/App_Data/chatbot_errors.log");
                    System.IO.File.AppendAllText(logPath, connError + "\n");
                }
                catch { /* Ignore logging errors */ }

                return Json(new ChatResponseViewModel
                {
                    Success = false,
                    Error = "Cannot connect to AI service",
                    Message = $"Không thể kết nối với AI service tại {AI_SERVICE_URL}. " +
                             "Vui lòng đảm bảo FastAPI đang chạy."
                });
            }
            catch (Exception ex)
            {
                // ✅ General error - LOG CHI TIẾT
                var generalError = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] GENERAL ERROR\n" +
                                  $"Exception: {ex.Message}\n" +
                                  $"StackTrace: {ex.StackTrace}\n";

                System.Diagnostics.Debug.WriteLine(generalError);

                // Log to file
                try
                {
                    var logPath = Server.MapPath("~/App_Data/chatbot_errors.log");
                    System.IO.File.AppendAllText(logPath, generalError + "\n");
                }
                catch { /* Ignore logging errors */ }

                return Json(new ChatResponseViewModel
                {
                    Success = false,
                    Error = ex.Message,
                    Message = "Xin lỗi, đã có lỗi xảy ra. Vui lòng thử lại sau."
                });
            }
        }

        // GET: /Chatbot/Health
        // Check AI service health status
        [HttpGet]
        public async Task<JsonResult> Health()
        {
            try
            {
                var response = await httpClient.GetAsync("/health");
                var content = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var healthData = JsonConvert.DeserializeObject<dynamic>(content);
                    return Json(new
                    {
                        success = true,
                        status = (string)healthData.status,
                        index_loaded = (bool)healthData.index_loaded,
                        total_products = (int)healthData.total_products
                    }, JsonRequestBehavior.AllowGet);
                }
                else
                {
                    return Json(new
                    {
                        success = false,
                        error = "AI service unhealthy"
                    }, JsonRequestBehavior.AllowGet);
                }
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    error = ex.Message
                }, JsonRequestBehavior.AllowGet);
            }
        }

        // POST: /Chatbot/RebuildIndex
        // Trigger AI service to rebuild product index | chỉ có Admin, tương lai phát triển tự động rebuild mỗi 7 ngày
        [HttpPost]
        [Authorize(Roles = "Administrator")]
        public async Task<JsonResult> RebuildIndex()
        {
            try
            {
                var response = await httpClient.PostAsync("/index-rebuild", null);
                var content = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var result = JsonConvert.DeserializeObject<dynamic>(content);
                    return Json(new
                    {
                        success = true,
                        message = (string)result.message,
                        products_indexed = (int)result.products_indexed
                    });
                }
                else
                {
                    return Json(new
                    {
                        success = false,
                        error = "Failed to rebuild index",
                        details = content
                    });
                }
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    error = ex.Message
                });
            }
        }

        // GET: /Chatbot/GetProductImage/{id}
        // Helper method để lấy product hình ảnh từ database
        [HttpGet]
        public JsonResult GetProductImage(int id)
        {
            try
            {
                var product = db.Products.Find(id);
                if (product == null)
                {
                    return Json(new
                    {
                        success = false,
                        error = "Product not found"
                    }, JsonRequestBehavior.AllowGet);
                }

                // Get main image
                var mainImage = db.ProductMedias
                    .Where(pm => pm.ProductID == id && pm.IsMain == true)
                    .Select(pm => pm.URL)
                    .FirstOrDefault();

                return Json(new
                {
                    success = true,
                    productId = id,
                    productName = product.ProductName,
                    imageUrl = mainImage ?? "/Content/images/no-image.jpg"
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    error = ex.Message
                }, JsonRequestBehavior.AllowGet);
            }
        }

        // GET: /Chatbot/QuickSuggestions
        // Lấy danh sách quick suggestion questions
        [HttpGet]
        public JsonResult QuickSuggestions()
        {
            var suggestions = new List<string>
            {
                "I want diamond engagement rings under 50 million",
                "Show me gold wedding bands",
                "What pearl necklaces do you have?",
                "I need earrings for a gift",
                "Looking for silver bracelets around 10 million"
            };

            return Json(new
            {
                success = true,
                suggestions = suggestions
            }, JsonRequestBehavior.AllowGet);
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