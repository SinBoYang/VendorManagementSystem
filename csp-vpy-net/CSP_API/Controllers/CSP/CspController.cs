using CSP_API.Services.CSP;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;

namespace CSP_API.Controllers.CSP
{
    [Route("csp/api")]
    [ApiController]
    [EnableCors("AllowLocalhostFrontend")]
    public class CspController : ControllerBase
    {
        private readonly ICspService _service;

        public CspController(ICspService service)
        {
            _service = service;
        }

        /// <summary>
        /// 範例程式
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPost("demoCode")]
        public async Task<ActionResult<Dictionary<string, object>>> DemoCode(
            [FromBody] Dictionary<string, object> request)
        {
            return Ok(await _service.DemoCode(request));
        }

        /// <summary>
        /// API-1：廠商查詢（模糊條件）
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPost("vendor/query")]
        public async Task<ActionResult<Dictionary<string, object>>> QueryVendor(
            [FromBody] Dictionary<string, object> request)
        {
            return Ok(await _service.QueryVendor(request));
        }

        /// <summary>
        /// API-2：廠商新增 / 更新
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPost("vendor/upsert")]
        public async Task<ActionResult<Dictionary<string, object>>> UpsertVendor(
            [FromBody] Dictionary<string, object> request)
        {
            return Ok(await _service.UpsertVendor(request));
        }

        /// <summary>
        /// API-3：取得下拉選單（廠商 / 稅率 / 幣別）
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPost("meta/options")]
        public async Task<ActionResult<Dictionary<string, object>>> QueryOptions(
            [FromBody] Dictionary<string, object> request)
        {
            return Ok(await _service.QueryOptions(request));
        }

        /// <summary>
        /// API-4：建立請款單
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPost("invoice/create")]
        public async Task<ActionResult<Dictionary<string, object>>> CreateInvoice(
            [FromBody] Dictionary<string, object> request)
        {
            return Ok(await _service.CreateInvoice(request));
        }

        /// <summary>
        /// API-5：請款單列表查詢
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPost("invoice/query")]
        public async Task<ActionResult<Dictionary<string, object>>> QueryInvoice(
            [FromBody] Dictionary<string, object> request)
        {
            return Ok(await _service.QueryInvoice(request));
        }

        /// <summary>
        /// API-6：請款單狀態異動（刪除 / 送審 / 核准 / 退回）
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPost("invoice/updateStatus")]
        public async Task<ActionResult<Dictionary<string, object>>> UpdateInvoiceStatus(
            [FromBody] Dictionary<string, object> request)
        {
            return Ok(await _service.UpdateInvoiceStatus(request));
        }

        /// <summary>
        /// API-7：付款登錄
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPost("payment/upsert")]
        public async Task<ActionResult<Dictionary<string, object>>> UpsertPayment(
            [FromBody] Dictionary<string, object> request)
        {
            return Ok(await _service.UpsertPayment(request));
        }

        /// <summary>
        /// API-8：匯出 Excel
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPost("invoice/export")]
        public async Task<ActionResult<Dictionary<string, object>>> ExportInvoice(
            [FromBody] Dictionary<string, object> request)
        {
            return Ok(await _service.ExportInvoice(request));
        }
    }
}