using System.Text.Json;
using CSP_API.Infrastructure;
using Microsoft.Data.SqlClient;
using NPOI.XSSF.UserModel;

namespace CSP_API.Services.CSP
{
    public class CspService : ICspService
    {
        private readonly ISqlConnectionFactory _sqlConnectionFactory;

        /// <summary>
        /// Constructor
        /// </summary>
        public CspService(ISqlConnectionFactory sqlConnectionFactory)
        {
            _sqlConnectionFactory = sqlConnectionFactory;
        }

        /// <summary>
        /// 範例程式
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public async Task<Dictionary<string, object>> DemoCode(Dictionary<string, object> request)
        {
            /* 1. 將前端傳入值取出：使用前端傳入物件的key值，從Map中取得對應value，例如： */
            string id = request.ContainsKey("id") ? request["id"]?.ToString() ?? string.Empty : string.Empty;
            string keyword = request.ContainsKey("keyword") ? request["keyword"]?.ToString() ?? string.Empty : string.Empty;
            Console.Error.WriteLine("id--->" + id);
            Console.Error.WriteLine("keyword--->" + keyword);

            /* 2. 業務邏輯：檢核、題目要求邏輯實作
            try (關鍵字：ADO.NET、using) {

            } catch (SQLException e) {
                e.printStackTrace();
            }
             */

            /* 3. 把要回傳給前端的值包裝成Map後return，例如： */
            var rtnMap = new Dictionary<string, object>();
            var mwHeader = new Dictionary<string, object>();
            mwHeader["MSGID"] = "DEMO001";
            rtnMap["MWHEADER"] = mwHeader;
            rtnMap["TRANRQ"] = null!;
            return await Task.FromResult(rtnMap);
        }

        /// <summary>
        /// API-1：廠商查詢（模糊條件）
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public async Task<Dictionary<string, object>> QueryVendor(Dictionary<string, object> request)
        {
            try
            {
                /* 1. 將前端傳入值取出 */
                Dictionary<string, object> tranrq = GetObject(request, "TRANRQ");
                string? taxId = NormalizeEmpty(GetString(tranrq, "TAX_ID"));
                string? vendorName = NormalizeEmpty(GetString(tranrq, "VENDOR_NAME"));
                string? status = NormalizeEmpty(GetString(tranrq, "STATUS"));

                /* 2. 模糊查詢廠商，條件為空代表不篩選 */
                string sql = @"
SELECT
    v.VENDOR_ID,
    v.VENDOR_NAME,
    v.TAX_ID,
    v.STATUS,
    v.EMAIL,
    v.PHONE,
    v.ADDRESS,
    v.BANK_CODE,
    v.ACCOUNT_NAME,
    v.ACCOUNT_NUMBER,
    b.BANK_NAME,
    v.CURRENCY_CODE
FROM dbo.VENDOR v
LEFT JOIN dbo.BANK b ON b.BANK_CODE = v.BANK_CODE
WHERE (@TAX_ID IS NULL OR v.TAX_ID LIKE '%' + @TAX_ID + '%')
  AND (@VENDOR_NAME IS NULL OR v.VENDOR_NAME LIKE '%' + @VENDOR_NAME + '%')
  AND (@STATUS IS NULL OR v.STATUS = @STATUS)
ORDER BY v.VENDOR_ID DESC;";

                List<Dictionary<string, object>> vendors = new List<Dictionary<string, object>>();

                using SqlConnection connection = _sqlConnectionFactory.CreateConnection();
                await connection.OpenAsync();

                using SqlCommand command = new SqlCommand(sql, connection);
                command.Parameters.AddWithValue("@TAX_ID", ToDbValue(taxId));
                command.Parameters.AddWithValue("@VENDOR_NAME", ToDbValue(vendorName));
                command.Parameters.AddWithValue("@STATUS", ToDbValue(status));

                using SqlDataReader reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    long vendorId = Convert.ToInt64(reader["VENDOR_ID"]);
                    string? bankCode = GetDbString(reader, "BANK_CODE");
                    string? accountNumber = GetDbString(reader, "ACCOUNT_NUMBER");

                    // 沒有銀行代碼就不回傳銀行資料
                    Dictionary<string, object>? bank = null;
                    if (!string.IsNullOrWhiteSpace(bankCode))
                    {
                        bank = new Dictionary<string, object>();
                        bank["BANK_CODE"] = bankCode;
                        bank["BANK_NAME"] = GetDbString(reader, "BANK_NAME") ?? string.Empty;
                        bank["ACCOUNT_NAME"] = GetDbString(reader, "ACCOUNT_NAME") ?? string.Empty;
                        bank["ACCOUNT_NUMBER_MASKED"] = MaskAccount(accountNumber);
                        bank["ACCOUNT_CCY"] = GetDbString(reader, "CURRENCY_CODE") ?? string.Empty;
                    }

                    Dictionary<string, object> vendor = new Dictionary<string, object>();
                    vendor["VENDOR_ID"] = vendorId;
                    vendor["VENDOR_CODE"] = "V" + vendorId.ToString("D4");
                    vendor["VENDOR_NAME"] = GetDbString(reader, "VENDOR_NAME") ?? string.Empty;
                    vendor["TAX_ID"] = GetDbString(reader, "TAX_ID") ?? string.Empty;
                    vendor["STATUS"] = GetDbString(reader, "STATUS") ?? string.Empty;
                    vendor["EMAIL"] = GetDbString(reader, "EMAIL") ?? string.Empty;
                    vendor["PHONE"] = GetDbString(reader, "PHONE") ?? string.Empty;
                    vendor["ADDRESS"] = GetDbString(reader, "ADDRESS") ?? string.Empty;
                    vendor["BANK"] = bank!;
                    vendors.Add(vendor);
                }

                /* 3. 把要回傳給前端的值包裝成 Map 後 return */
                Dictionary<string, object> tranrs = new Dictionary<string, object>();
                tranrs["VENDORS"] = vendors;
                return BuildResponse(request, "0000", "交易成功", tranrs);
            }
            catch (Exception ex)
            {
                Dictionary<string, object> tranrs = new Dictionary<string, object>();
                tranrs["VENDORS"] = new List<Dictionary<string, object>>();
                return BuildResponse(request, "9999", ex.Message, tranrs);
            }
        }

        /// <summary>
        /// API-2：廠商新增 / 更新
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public async Task<Dictionary<string, object>> UpsertVendor(Dictionary<string, object> request)
        {
            try
            {
                /* 1. 將前端傳入值取出 */
                Dictionary<string, object> tranrq = GetObject(request, "TRANRQ");
                Dictionary<string, object> vendor = GetObject(tranrq, "VENDOR");
                Dictionary<string, object> bank = GetObject(vendor, "BANK");

                string? vendorId = NormalizeEmpty(GetString(vendor, "VENDOR_ID"));
                string? taxId = NormalizeEmpty(GetString(vendor, "TAX_ID"));
                string? vendorName = NormalizeEmpty(GetString(vendor, "VENDOR_NAME"));
                string? status = NormalizeEmpty(GetString(vendor, "STATUS"));
                string? email = NormalizeEmpty(GetString(vendor, "EMAIL"));
                string? phone = NormalizeEmpty(GetString(vendor, "PHONE"));
                string? address = NormalizeEmpty(GetString(vendor, "ADDRESS"));
                string? bankCode = NormalizeEmpty(GetString(bank, "BANK_CODE"));
                string? accountName = NormalizeEmpty(GetString(bank, "ACCOUNT_NAME"));
                string? accountNumber = NormalizeEmpty(GetString(bank, "ACCOUNT_NUMBER"));
                string? accountCcy = NormalizeEmpty(GetString(bank, "ACCOUNT_CCY"));

                /* 2. 檢查欄位，格式不對回傳 E001 */
                if (taxId == null || vendorName == null)
                {
                    return BuildResponse(request, "E001", "統一編號與廠商名稱為必填");
                }
                if (status != "ACTIVE" && status != "INACTIVE")
                {
                    return BuildResponse(request, "E001", "狀態必須是 ACTIVE 或 INACTIVE");
                }

                long vendorIdNumber = 0;
                if (vendorId != null && !long.TryParse(vendorId, out vendorIdNumber))
                {
                    return BuildResponse(request, "E001", "VENDOR_ID 格式錯誤");
                }

                /* 3. VENDOR_ID 空 = 新增；有值 = 更新 */
                string sql;
                if (vendorId == null)
                {
                    sql = @"
INSERT INTO dbo.VENDOR
    (VENDOR_NAME, TAX_ID, STATUS, EMAIL, PHONE, ADDRESS, BANK_CODE, ACCOUNT_NAME, ACCOUNT_NUMBER, CURRENCY_CODE)
VALUES
    (@VENDOR_NAME, @TAX_ID, @STATUS, @EMAIL, @PHONE, @ADDRESS, @BANK_CODE, @ACCOUNT_NAME, @ACCOUNT_NUMBER, @CURRENCY_CODE);";
                }
                else
                {
                    sql = @"
UPDATE dbo.VENDOR
SET VENDOR_NAME = @VENDOR_NAME,
    TAX_ID = @TAX_ID,
    STATUS = @STATUS,
    EMAIL = @EMAIL,
    PHONE = @PHONE,
    ADDRESS = @ADDRESS,
    BANK_CODE = @BANK_CODE,
    ACCOUNT_NAME = @ACCOUNT_NAME,
    ACCOUNT_NUMBER = COALESCE(@ACCOUNT_NUMBER, ACCOUNT_NUMBER),
    CURRENCY_CODE = @CURRENCY_CODE
WHERE VENDOR_ID = @VENDOR_ID;";
                }

                using SqlConnection connection = _sqlConnectionFactory.CreateConnection();
                await connection.OpenAsync();

                using SqlCommand command = new SqlCommand(sql, connection);
                command.Parameters.AddWithValue("@VENDOR_NAME", vendorName);
                command.Parameters.AddWithValue("@TAX_ID", taxId);
                command.Parameters.AddWithValue("@STATUS", status);
                command.Parameters.AddWithValue("@EMAIL", ToDbValue(email));
                command.Parameters.AddWithValue("@PHONE", ToDbValue(phone));
                command.Parameters.AddWithValue("@ADDRESS", ToDbValue(address));
                command.Parameters.AddWithValue("@BANK_CODE", ToDbValue(bankCode));
                command.Parameters.AddWithValue("@ACCOUNT_NAME", ToDbValue(accountName));
                command.Parameters.AddWithValue("@ACCOUNT_NUMBER", ToDbValue(accountNumber));
                command.Parameters.AddWithValue("@CURRENCY_CODE", ToDbValue(accountCcy));
                if (vendorId != null)
                {
                    command.Parameters.AddWithValue("@VENDOR_ID", vendorIdNumber);
                }

                int affectedRows = await command.ExecuteNonQueryAsync();
                if (affectedRows == 0)
                {
                    return BuildResponse(request, "E702", "查無資料");
                }

                /* 4. 把要回傳給前端的值包裝成 Map 後 return */
                return BuildResponse(request, "0000", "交易成功");
            }
            catch (Exception ex)
            {
                return BuildResponse(request, "9999", ex.Message);
            }
        }

        /// <summary>
        /// API-3：取得下拉選單（廠商 / 稅率 / 幣別）
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public async Task<Dictionary<string, object>> QueryOptions(Dictionary<string, object> request)
        {
            try
            {
                /* 1. 此 API 不需要前端傳入條件 */
                List<Dictionary<string, object>> currencies = new List<Dictionary<string, object>>();
                List<Dictionary<string, object>> taxRates = new List<Dictionary<string, object>>();
                List<Dictionary<string, object>> vendors = new List<Dictionary<string, object>>();

                using SqlConnection connection = _sqlConnectionFactory.CreateConnection();
                await connection.OpenAsync();

                /* 2-1. 幣別 */
                string currencySql = "SELECT CURRENCY_CODE, CURRENCY_NAME FROM dbo.CURRENCY ORDER BY CURRENCY_CODE;";
                using (SqlCommand command = new SqlCommand(currencySql, connection))
                using (SqlDataReader reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        Dictionary<string, object> currency = new Dictionary<string, object>();
                        currency["CODE"] = GetDbString(reader, "CURRENCY_CODE") ?? string.Empty;
                        currency["NAME"] = GetDbString(reader, "CURRENCY_NAME") ?? string.Empty;
                        currencies.Add(currency);
                    }
                }

                /* 2-2. 稅率 */
                string taxSql = "SELECT TAX_CODE, TAX_PERCENT FROM dbo.TAX ORDER BY TAX_PERCENT;";
                using (SqlCommand command = new SqlCommand(taxSql, connection))
                using (SqlDataReader reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        Dictionary<string, object> tax = new Dictionary<string, object>();
                        tax["CODE"] = GetDbString(reader, "TAX_CODE") ?? string.Empty;
                        tax["PERCENT"] = Convert.ToDecimal(reader["TAX_PERCENT"]);
                        taxRates.Add(tax);
                    }
                }

                /* 2-3. 廠商（只列出啟用中的） */
                string vendorSql = "SELECT VENDOR_ID, VENDOR_NAME FROM dbo.VENDOR WHERE STATUS = N'ACTIVE' ORDER BY VENDOR_ID;";
                using (SqlCommand command = new SqlCommand(vendorSql, connection))
                using (SqlDataReader reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        long vendorId = Convert.ToInt64(reader["VENDOR_ID"]);
                        Dictionary<string, object> vendor = new Dictionary<string, object>();
                        vendor["VENDOR_ID"] = vendorId.ToString();
                        vendor["VENDOR_CODE"] = "V" + vendorId.ToString("D4");
                        vendor["VENDOR_NAME"] = GetDbString(reader, "VENDOR_NAME") ?? string.Empty;
                        vendors.Add(vendor);
                    }
                }

                /* 3. 把要回傳給前端的值包裝成 Map 後 return */
                Dictionary<string, object> tranrs = new Dictionary<string, object>();
                tranrs["CURRENCIES"] = currencies;
                tranrs["TAX_RATES"] = taxRates;
                tranrs["VENDORS"] = vendors;
                return BuildResponse(request, "0000", "交易成功", tranrs);
            }
            catch (Exception ex)
            {
                return BuildResponse(request, "9999", ex.Message);
            }
        }

        /// <summary>
        /// API-4：建立請款單
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public async Task<Dictionary<string, object>> CreateInvoice(Dictionary<string, object> request)
        {
            try
            {
                /* 1. 將前端傳入值取出 */
                Dictionary<string, object> tranrq = GetObject(request, "TRANRQ");
                Dictionary<string, object> invoice = GetObject(tranrq, "INVOICE");

                string? item = NormalizeEmpty(GetString(invoice, "ITEM"));
                string? currencyCode = NormalizeEmpty(GetString(invoice, "CURRENCY_CODE"));
                string? taxCode = NormalizeEmpty(GetString(invoice, "TAX_CODE"));
                string? vendorIdText = NormalizeEmpty(GetString(invoice, "VENDOR_ID"));
                string? subtotalText = NormalizeEmpty(GetString(invoice, "SUBTOTAL"));
                string? invoiceDateText = NormalizeEmpty(GetString(invoice, "INVOICE_DATE"));
                string? dueDateText = NormalizeEmpty(GetString(invoice, "DUE_DATE"));
                string? remarks = NormalizeEmpty(GetString(invoice, "REMARKS"));

                /* 2. 檢查欄位，格式不對回傳 E001 */
                if (item == null || currencyCode == null || taxCode == null)
                {
                    return BuildResponse(request, "E001", "項目、幣別、稅率為必填");
                }

                long vendorId;
                if (!long.TryParse(vendorIdText, out vendorId))
                {
                    return BuildResponse(request, "E001", "廠商格式錯誤");
                }

                decimal subtotal;
                if (!decimal.TryParse(subtotalText, out subtotal) || subtotal < 0)
                {
                    return BuildResponse(request, "E001", "未稅金額格式錯誤");
                }

                DateTime invoiceDate;
                if (!DateTime.TryParse(invoiceDateText, out invoiceDate))
                {
                    return BuildResponse(request, "E001", "請款日格式錯誤");
                }

                // 到期日可以不填，有填才檢查格式
                DateTime? dueDate = null;
                if (dueDateText != null)
                {
                    DateTime parsedDueDate;
                    if (!DateTime.TryParse(dueDateText, out parsedDueDate))
                    {
                        return BuildResponse(request, "E001", "到期日格式錯誤");
                    }
                    dueDate = parsedDueDate;
                }

                using SqlConnection connection = _sqlConnectionFactory.CreateConnection();
                await connection.OpenAsync();

                /* 3-1. 查稅率 */
                string taxSql = "SELECT TAX_PERCENT FROM dbo.TAX WHERE TAX_CODE = @TAX_CODE;";
                object? taxResult;
                using (SqlCommand taxCommand = new SqlCommand(taxSql, connection))
                {
                    taxCommand.Parameters.AddWithValue("@TAX_CODE", taxCode);
                    taxResult = await taxCommand.ExecuteScalarAsync();
                }
                if (taxResult == null)
                {
                    return BuildResponse(request, "E702", "查無稅率資料");
                }
                decimal taxPercent = Convert.ToDecimal(taxResult);

                /* 3-2. 含稅金額 = 未稅金額 * (1 + 稅率) */
                decimal total = Math.Round(subtotal * (1 + taxPercent / 100), 2);

                /* 3-3. 產生請款單號：INV-年月-三碼流水號，例如 INV-202509-001 */
                string prefix = "INV-" + invoiceDate.ToString("yyyyMM") + "-";
                string countSql = "SELECT COUNT(*) FROM dbo.INVOICE WHERE INVOICE_NO LIKE @PREFIX + '%';";
                int count;
                using (SqlCommand countCommand = new SqlCommand(countSql, connection))
                {
                    countCommand.Parameters.AddWithValue("@PREFIX", prefix);
                    count = Convert.ToInt32(await countCommand.ExecuteScalarAsync());
                }
                string invoiceNo = prefix + (count + 1).ToString("D3");

                /* 3-4. 新增請款單，狀態預設為草稿 DRAFT */
                string insertSql = @"
INSERT INTO dbo.INVOICE
    (INVOICE_NO, ITEM, CURRENCY_CODE, TAX_CODE, VENDOR_ID, INVOICE_DATE, DUE_DATE, REMARKS, STATUS, SUBTOTAL, TOTAL)
VALUES
    (@INVOICE_NO, @ITEM, @CURRENCY_CODE, @TAX_CODE, @VENDOR_ID, @INVOICE_DATE, @DUE_DATE, @REMARKS, N'DRAFT', @SUBTOTAL, @TOTAL);";

                using (SqlCommand insertCommand = new SqlCommand(insertSql, connection))
                {
                    insertCommand.Parameters.AddWithValue("@INVOICE_NO", invoiceNo);
                    insertCommand.Parameters.AddWithValue("@ITEM", item);
                    insertCommand.Parameters.AddWithValue("@CURRENCY_CODE", currencyCode);
                    insertCommand.Parameters.AddWithValue("@TAX_CODE", taxCode);
                    insertCommand.Parameters.AddWithValue("@VENDOR_ID", vendorId);
                    insertCommand.Parameters.AddWithValue("@INVOICE_DATE", invoiceDate);
                    insertCommand.Parameters.AddWithValue("@DUE_DATE", ToDbValue(dueDate));
                    insertCommand.Parameters.AddWithValue("@REMARKS", ToDbValue(remarks));
                    insertCommand.Parameters.AddWithValue("@SUBTOTAL", subtotal);
                    insertCommand.Parameters.AddWithValue("@TOTAL", total);
                    await insertCommand.ExecuteNonQueryAsync();
                }

                /* 4. 把要回傳給前端的值包裝成 Map 後 return */
                return BuildResponse(request, "0000", "交易成功");
            }
            catch (Exception ex)
            {
                return BuildResponse(request, "9999", ex.Message);
            }
        }

        /// <summary>
        /// API-5：請款單列表查詢
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public async Task<Dictionary<string, object>> QueryInvoice(Dictionary<string, object> request)
        {
            try
            {
                /* 1. 將前端傳入值取出 */
                Dictionary<string, object> tranrq = GetObject(request, "TRANRQ");
                string? status = NormalizeEmpty(GetString(tranrq, "STATUS"));

                /* 2. 查詢資料庫 */
                List<Dictionary<string, object>> invoiceList = await GetInvoiceList(status);

                /* 3. 把要回傳給前端的值包裝成 Map 後 return */
                Dictionary<string, object> tranrs = new Dictionary<string, object>();
                tranrs["INVOICE_LIST"] = invoiceList;
                return BuildResponse(request, "0000", "交易成功", tranrs);
            }
            catch (Exception ex)
            {
                return BuildResponse(request, "9999", ex.Message);
            }
        }

        /// <summary>
        /// API-6：請款單狀態異動（刪除 / 送審 / 核准 / 退回）
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public async Task<Dictionary<string, object>> UpdateInvoiceStatus(
            Dictionary<string, object> request)
        {
            try
            {
                /* 1. 將前端傳入值取出 */
                Dictionary<string, object> tranrq = GetObject(request, "TRANRQ");
                string? invoiceIdText = NormalizeEmpty(GetString(tranrq, "INVOICE_ID"));
                string? action = NormalizeEmpty(GetString(tranrq, "ACTION"));

                /* 2. 依 ACTION 決定「目前必須是什麼狀態」以及「要改成什麼狀態」 */
                string requiredStatus;
                string newStatus;
                if (action == "DELETE")
                {
                    requiredStatus = "DRAFT";
                    newStatus = "DELETED";
                }
                else if (action == "SUBMIT")
                {
                    requiredStatus = "DRAFT";
                    newStatus = "SUBMITTED";
                }
                else if (action == "APPROVE")
                {
                    requiredStatus = "SUBMITTED";
                    newStatus = "APPROVED";
                }
                else if (action == "REJECT")
                {
                    requiredStatus = "SUBMITTED";
                    newStatus = "REJECTED";
                }
                else
                {
                    return BuildResponse(request, "E001", "ACTION 不正確");
                }

                long invoiceId;
                if (!long.TryParse(invoiceIdText, out invoiceId))
                {
                    return BuildResponse(request, "E001", "INVOICE_ID 格式錯誤");
                }

                using SqlConnection connection = _sqlConnectionFactory.CreateConnection();
                await connection.OpenAsync();

                /* 3-1. 查出目前狀態 */
                string selectSql = "SELECT STATUS FROM dbo.INVOICE WHERE INVOICE_ID = @INVOICE_ID;";
                object? statusResult;
                using (SqlCommand selectCommand = new SqlCommand(selectSql, connection))
                {
                    selectCommand.Parameters.AddWithValue("@INVOICE_ID", invoiceId);
                    statusResult = await selectCommand.ExecuteScalarAsync();
                }
                if (statusResult == null)
                {
                    return BuildResponse(request, "E702", "查無資料");
                }
                string oldStatus = statusResult.ToString() ?? string.Empty;

                /* 3-2. 目前狀態不符合就不能異動 */
                if (oldStatus != requiredStatus)
                {
                    return BuildResponse(request, "E001", "目前狀態為 " + oldStatus + "，無法執行 " + action);
                }

                /* 3-3. 更新狀態 */
                string updateSql = "UPDATE dbo.INVOICE SET STATUS = @NEW_STATUS, UPDATED_AT = SYSDATETIME() WHERE INVOICE_ID = @INVOICE_ID;";
                using (SqlCommand updateCommand = new SqlCommand(updateSql, connection))
                {
                    updateCommand.Parameters.AddWithValue("@NEW_STATUS", newStatus);
                    updateCommand.Parameters.AddWithValue("@INVOICE_ID", invoiceId);
                    await updateCommand.ExecuteNonQueryAsync();
                }

                /* 4. 把要回傳給前端的值包裝成 Map 後 return */
                Dictionary<string, object> tranrs = new Dictionary<string, object>();
                tranrs["INVOICE_ID"] = invoiceId;
                tranrs["OLD_STATUS"] = oldStatus;
                tranrs["NEW_STATUS"] = newStatus;
                return BuildResponse(request, "0000", "交易成功", tranrs);
            }
            catch (Exception ex)
            {
                return BuildResponse(request, "9999", ex.Message);
            }
        }

        /// <summary>
        /// API-7：付款登錄
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public async Task<Dictionary<string, object>> UpsertPayment(Dictionary<string, object> request)
        {
            try
            {
                /* 1. 將前端傳入值取出 */
                Dictionary<string, object> tranrq = GetObject(request, "TRANRQ");
                Dictionary<string, object> payment = GetObject(tranrq, "PAYMENT");

                string? invoiceIdText = NormalizeEmpty(GetString(payment, "INVOICE_ID"));
                string? paymentDateText = NormalizeEmpty(GetString(payment, "PAYMENT_DATE"));
                string? payerRemark = NormalizeEmpty(GetString(payment, "PAYER_REMARK"));

                /* 2. 檢查欄位，格式不對回傳 E001 */
                long invoiceId;
                if (!long.TryParse(invoiceIdText, out invoiceId))
                {
                    return BuildResponse(request, "E001", "INVOICE_ID 格式錯誤");
                }

                DateTime paymentDate;
                if (!DateTime.TryParse(paymentDateText, out paymentDate))
                {
                    return BuildResponse(request, "E001", "付款日期格式錯誤");
                }

                using SqlConnection connection = _sqlConnectionFactory.CreateConnection();
                await connection.OpenAsync();

                // 新增付款單與更新請款單狀態要一起成功，所以放在同一個交易
                // 沒有 Commit 就離開時，交易會自動 Rollback
                using SqlTransaction transaction = connection.BeginTransaction();

                /* 3-1. 查出請款單目前狀態，必須是送審 SUBMITTED 才能付款 */
                string selectSql = "SELECT STATUS FROM dbo.INVOICE WHERE INVOICE_ID = @INVOICE_ID;";
                object? statusResult;
                using (SqlCommand selectCommand = new SqlCommand(selectSql, connection, transaction))
                {
                    selectCommand.Parameters.AddWithValue("@INVOICE_ID", invoiceId);
                    statusResult = await selectCommand.ExecuteScalarAsync();
                }
                if (statusResult == null)
                {
                    return BuildResponse(request, "E702", "查無資料");
                }
                string status = statusResult.ToString() ?? string.Empty;
                if (status != "SUBMITTED")
                {
                    return BuildResponse(request, "E001", "目前狀態為 " + status + "，無法付款");
                }

                /* 3-2. 新增付款單 */
                string insertSql = "INSERT INTO dbo.PAYMENT (INVOICE_ID, PAYER_REMARK, PAYMENTDATE) VALUES (@INVOICE_ID, @PAYER_REMARK, @PAYMENTDATE);";
                using (SqlCommand insertCommand = new SqlCommand(insertSql, connection, transaction))
                {
                    insertCommand.Parameters.AddWithValue("@INVOICE_ID", invoiceId);
                    insertCommand.Parameters.AddWithValue("@PAYER_REMARK", ToDbValue(payerRemark));
                    insertCommand.Parameters.AddWithValue("@PAYMENTDATE", paymentDate);
                    await insertCommand.ExecuteNonQueryAsync();
                }

                /* 3-3. 更新請款單狀態（資料表的狀態限制沒有 PAID，所以用 APPROVED 表示已付款） */
                string updateSql = "UPDATE dbo.INVOICE SET STATUS = N'APPROVED', UPDATED_AT = SYSDATETIME() WHERE INVOICE_ID = @INVOICE_ID;";
                using (SqlCommand updateCommand = new SqlCommand(updateSql, connection, transaction))
                {
                    updateCommand.Parameters.AddWithValue("@INVOICE_ID", invoiceId);
                    await updateCommand.ExecuteNonQueryAsync();
                }

                transaction.Commit();

                /* 4. 把要回傳給前端的值包裝成 Map 後 return */
                return BuildResponse(request, "0000", "交易成功");
            }
            catch (Exception ex)
            {
                return BuildResponse(request, "9999", ex.Message);
            }
        }

        /// <summary>
        /// API-8：匯出 Excel
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public async Task<Dictionary<string, object>> ExportInvoice(Dictionary<string, object> request)
        {
            try
            {
                /* 1. 查詢送審 SUBMITTED 的請款單（與 API-5 使用同一段查詢） */
                List<Dictionary<string, object>> invoiceList = await GetInvoiceList("SUBMITTED");

                /* 2-1. 建立活頁簿與工作表 */
                using XSSFWorkbook workbook = new XSSFWorkbook();
                var sheet = workbook.CreateSheet("請款單列表");

                /* 2-2. 建立表頭 */
                string[] titles = { "請款單號", "廠商", "項目", "幣別", "未稅金額", "含稅金額", "請款日", "到期日", "備註" };
                var headerRow = sheet.CreateRow(0);
                for (int i = 0; i < titles.Length; i++)
                {
                    headerRow.CreateCell(i).SetCellValue(titles[i]);
                }

                /* 2-3. 建立內容，第 0 列是表頭，所以資料從第 1 列開始 */
                for (int i = 0; i < invoiceList.Count; i++)
                {
                    Dictionary<string, object> invoice = invoiceList[i];
                    var dataRow = sheet.CreateRow(i + 1);
                    dataRow.CreateCell(0).SetCellValue(invoice["INVOICE_NO"].ToString());
                    dataRow.CreateCell(1).SetCellValue(invoice["VENDOR_NAME"].ToString());
                    dataRow.CreateCell(2).SetCellValue(invoice["ITEM"].ToString());
                    dataRow.CreateCell(3).SetCellValue(invoice["CURRENCY_CODE"].ToString());
                    dataRow.CreateCell(4).SetCellValue(Convert.ToDouble(invoice["SUBTOTAL"]));
                    dataRow.CreateCell(5).SetCellValue(Convert.ToDouble(invoice["TOTAL"]));
                    dataRow.CreateCell(6).SetCellValue(invoice["INVOICE_DATE"].ToString());
                    dataRow.CreateCell(7).SetCellValue(invoice["DUE_DATE"].ToString());
                    dataRow.CreateCell(8).SetCellValue(invoice["REMARKS"].ToString());
                }

                /* 3. 把活頁簿寫成 byte[]，轉成 Base64 字串回傳（前端會再轉回檔案） */
                using MemoryStream stream = new MemoryStream();
                workbook.Write(stream, true);
                string base64 = Convert.ToBase64String(stream.ToArray());

                List<string> data = new List<string>();
                data.Add(base64);
                Dictionary<string, object> tranrs = new Dictionary<string, object>();
                tranrs["DATA"] = data;
                return BuildResponse(request, "0000", "交易成功", tranrs);
            }
            catch (Exception ex)
            {
                return BuildResponse(request, "9999", ex.Message);
            }
        }

        /// <summary>
        /// 查詢請款單列表（API-5 與 API-8 共用），status 為 null 代表查全部
        /// </summary>
        private async Task<List<Dictionary<string, object>>> GetInvoiceList(string? status)
        {
            List<Dictionary<string, object>> invoiceList = new List<Dictionary<string, object>>();

            string sql = @"
SELECT
    i.INVOICE_ID,
    i.INVOICE_NO,
    v.VENDOR_NAME,
    i.ITEM,
    i.CURRENCY_CODE,
    i.SUBTOTAL,
    i.TOTAL,
    i.INVOICE_DATE,
    i.DUE_DATE,
    i.REMARKS
FROM dbo.INVOICE i
JOIN dbo.VENDOR v ON v.VENDOR_ID = i.VENDOR_ID
WHERE (@STATUS IS NULL OR i.STATUS = @STATUS)
ORDER BY i.INVOICE_ID DESC;";

            using SqlConnection connection = _sqlConnectionFactory.CreateConnection();
            await connection.OpenAsync();

            using SqlCommand command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@STATUS", ToDbValue(status));

            using SqlDataReader reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                // 到期日可能是空的
                string dueDate = string.Empty;
                if (reader["DUE_DATE"] != DBNull.Value)
                {
                    dueDate = Convert.ToDateTime(reader["DUE_DATE"]).ToString("yyyy-MM-dd");
                }

                Dictionary<string, object> invoice = new Dictionary<string, object>();
                invoice["INVOICE_ID"] = Convert.ToInt64(reader["INVOICE_ID"]);
                invoice["INVOICE_NO"] = GetDbString(reader, "INVOICE_NO") ?? string.Empty;
                invoice["VENDOR_NAME"] = GetDbString(reader, "VENDOR_NAME") ?? string.Empty;
                invoice["ITEM"] = GetDbString(reader, "ITEM") ?? string.Empty;
                invoice["CURRENCY_CODE"] = GetDbString(reader, "CURRENCY_CODE") ?? string.Empty;
                invoice["SUBTOTAL"] = Convert.ToDecimal(reader["SUBTOTAL"]);
                invoice["TOTAL"] = Convert.ToDecimal(reader["TOTAL"]);
                invoice["INVOICE_DATE"] = Convert.ToDateTime(reader["INVOICE_DATE"]).ToString("yyyy-MM-dd");
                invoice["DUE_DATE"] = dueDate;
                invoice["REMARKS"] = GetDbString(reader, "REMARKS") ?? string.Empty;
                invoiceList.Add(invoice);
            }

            return invoiceList;
        }

        /// <summary>
        /// 組出回傳格式：MWHEADER（MSGID、RtnCode、RtnDesc）+ TRANRS，不需要回傳資料時使用
        /// </summary>
        private static Dictionary<string, object> BuildResponse(
            Dictionary<string, object> request, string rtnCode, string rtnDesc)
        {
            return BuildResponse(request, rtnCode, rtnDesc, new Dictionary<string, object>());
        }

        /// <summary>
        /// 組出回傳格式：MWHEADER（MSGID、RtnCode、RtnDesc）+ TRANRS
        /// </summary>
        private static Dictionary<string, object> BuildResponse(
            Dictionary<string, object> request, string rtnCode, string rtnDesc, Dictionary<string, object> tranrs)
        {
            Dictionary<string, object> mwHeader = new Dictionary<string, object>();
            mwHeader["MSGID"] = GetMsgId(request);
            mwHeader["RtnCode"] = rtnCode;
            mwHeader["RtnDesc"] = rtnDesc;

            Dictionary<string, object> rtnMap = new Dictionary<string, object>();
            rtnMap["MWHEADER"] = mwHeader;
            rtnMap["TRANRS"] = tranrs;
            return rtnMap;
        }

        /// <summary>
        /// 寫入資料庫的參數值：null 要轉成 DBNull.Value
        /// </summary>
        private static object ToDbValue(object? value)
        {
            if (value == null)
            {
                return DBNull.Value;
            }
            return value;
        }

        private static string GetMsgId(Dictionary<string, object> request)
        {
            var mw = GetObject(request, "MWHEADER");
            return GetString(mw, "MSGID") ?? string.Empty;
        }

        private static Dictionary<string, object> GetObject(Dictionary<string, object> source, string key)
        {
            
            Dictionary<string, object> result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);  
            if (!source.ContainsKey(key))
            {
                return result;
            }

            object value = source[key];
            if (value == null)
            {
                return result;
            }
            if (value is Dictionary<string, object>)
            {
                return (Dictionary<string, object>)value;
            }

            if (value is JsonElement)
            {
                JsonElement element = (JsonElement)value;
                if (element.ValueKind == JsonValueKind.Object)
                {
                    foreach (JsonProperty property in element.EnumerateObject())
                    {
                        result[property.Name] = property.Value;
                    }
                }
            }
            return result;
        }

        private static string? GetString(Dictionary<string, object> source, string key)
        {
            if (!source.ContainsKey(key))
            {
                return null;
            }

            object value = source[key];

            if (value == null)
            {
                return null;
            }

            if (value is string)
            {
                return (string)value;
            }

            if (value is JsonElement)
            {
                JsonElement element = (JsonElement)value;

                if (element.ValueKind == JsonValueKind.Null)
                {
                    return null;
                }
                if (element.ValueKind == JsonValueKind.String)
                {
                    return element.GetString();
                }
                if (element.ValueKind == JsonValueKind.True)
                {
                    return "true";
                }
                if (element.ValueKind == JsonValueKind.False)
                {
                    return "false";
                }

                return element.GetRawText();
            }

            return value.ToString();
        }

        private static string? GetDbString(SqlDataReader reader, string column)
        {
            var idx = reader.GetOrdinal(column);
            return reader.IsDBNull(idx) ? null : reader.GetString(idx);
        }

        private static string? NormalizeEmpty(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private static string MaskAccount(string? account)
        {
            if (string.IsNullOrWhiteSpace(account))
                return string.Empty;

            var text = account.Trim();
            if (text.Length <= 4)
                return new string('*', text.Length);

            return new string('*', text.Length - 4) + text[^4..];
        }
    }
}
