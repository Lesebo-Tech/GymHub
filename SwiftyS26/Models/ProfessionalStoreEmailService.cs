using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using MimeKit.Utils;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Web.Configuration;

namespace SwiftyS26.Models
{
    public sealed class StoreEmailLineItem
    {
        public string ProductName { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineTotal { get; set; }
    }

    public static class ProfessionalStoreEmailService
    {
        private static string SenderName => Setting("GymHubEmail:SenderName", "GymHub");
        private static string SenderEmail => Setting("GymHubEmail:SenderEmail", null);
        private static string AppPassword => Setting("GymHubEmail:AppPassword", null);
        private static string SmtpHost => Setting("GymHubEmail:SmtpHost", "smtp.gmail.com");
        private static string SupportEmail => Setting("GymHubEmail:SupportEmail", "support@gymhub.co.za");
        private static string BaseUrl => Setting("GymHubEmail:BaseUrl", "https://localhost:44394").TrimEnd('/');

        private static int SmtpPort
        {
            get
            {
                int port;
                return int.TryParse(Setting("GymHubEmail:SmtpPort", "587"), out port) ? port : 587;
            }
        }

        // Keeps your existing controller call working.
        public static void SendOrderPlaced(
            string toEmail,
            string toName,
            string referenceNumber,
            decimal finalTotal)
        {
            SendOrderPlaced(
                toEmail,
                toName,
                referenceNumber,
                finalTotal,
                0m,
                finalTotal,
                null,
                DateTime.Now,
                null);
        }

        // Preferred itemised order-confirmation overload.
        public static void SendOrderPlaced(
            string toEmail,
            string toName,
            string referenceNumber,
            decimal subtotal,
            decimal discountAmount,
            decimal finalTotal,
            IEnumerable<StoreEmailLineItem> items,
            DateTime paidAt,
            string paystackReference)
        {
            var safeItems = (items ?? Enumerable.Empty<StoreEmailLineItem>()).ToList();
            string orderUrl = BaseUrl + "/Store/MyOrders";

            string itemRows = safeItems.Any()
                ? string.Join("", safeItems.Select(i =>
                    Row(
                        $"{H(i.ProductName)} &times; {i.Quantity}",
                        Money(i.LineTotal))))
                : Row("Order total", Money(finalTotal));

            string discountRow = discountAmount > 0m
                ? Row("Member discount", "-&nbsp;" + Money(discountAmount), "#15803d")
                : string.Empty;

            string content =
                Greeting(toName) +
                Paragraph("Your payment was successful and your GymHub store order has been confirmed. Our store team will now prepare it for collection.") +
                StatusBanner("Payment confirmed", "Your order is being prepared", "#eff6ff", "#1d4ed8") +
                DetailsTable(
                    Row("Order reference", H(referenceNumber)) +
                    Row("Payment date", H(paidAt.ToString("dd MMM yyyy, HH:mm"))) +
                    (!string.IsNullOrWhiteSpace(paystackReference)
                        ? Row("Payment reference", H(paystackReference))
                        : string.Empty) +
                    itemRows +
                    Row("Subtotal", Money(subtotal)) +
                    discountRow +
                    TotalRow("Total paid", Money(finalTotal))) +
                Notice("What happens next?", "We will email you when the order is ready. Bring your collection QR and identification to the GymHub store counter.", "#fffbeb", "#92400e") +
                Button("View my orders", orderUrl);

            var builder = CreateBuilder(content, "Order confirmation");

            if (safeItems.Any())
            {
                string invoice = BuildInvoiceHtml(
                    toName,
                    referenceNumber,
                    paidAt,
                    paystackReference,
                    subtotal,
                    discountAmount,
                    finalTotal,
                    safeItems);

                builder.Attachments.Add(
                    SafeFileName("GymHub-Invoice-" + referenceNumber + ".html"),
                    Encoding.UTF8.GetBytes(invoice),
                    ContentType.Parse("text/html; charset=utf-8"));
            }

            SendMessage(
                toEmail,
                toName,
                "Order confirmed | " + referenceNumber,
                builder);
        }

        public static void SendOrderReady(
            string toEmail,
            string toName,
            string referenceNumber,
            string qrBase64)
        {
            string qrHtml = string.Empty;
            var builder = new BodyBuilder();

            if (!string.IsNullOrWhiteSpace(qrBase64))
            {
                try
                {
                    byte[] qrBytes = Convert.FromBase64String(qrBase64);
                    var qr = builder.LinkedResources.Add(
                        "gymhub-pickup-qr.png",
                        qrBytes,
                        ContentType.Parse("image/png"));

                    qr.ContentId = MimeUtils.GenerateMessageId();
                    qr.ContentDisposition = new ContentDisposition(ContentDisposition.Inline);

                    qrHtml = QrPanel(qr.ContentId, referenceNumber, "Collection QR code");
                }
                catch (FormatException ex)
                {
                    System.Diagnostics.Debug.WriteLine("[EMAIL QR ERROR] " + ex.Message);
                }
            }

            string content =
                Greeting(toName) +
                StatusBanner("Ready for collection", "Your GymHub order is ready", "#ecfdf5", "#047857") +
                Paragraph("Present the QR code below to Store Staff when collecting your order.") +
                DetailsTable(Row("Order reference", H(referenceNumber))) +
                qrHtml +
                Notice("Keep your QR secure", "Do not share this QR with anyone except authorised GymHub Store Staff.", "#fffbeb", "#92400e") +
                Button("View my orders", BaseUrl + "/Store/MyOrders");

            builder.HtmlBody = Wrap(content, "Order ready for collection");

            SendMessage(
                toEmail,
                toName,
                "Ready for collection | " + referenceNumber,
                builder);
        }

        public static void SendOrderFirstReminder(
            string toEmail,
            string toName,
            string referenceNumber)
        {
            SendCollectionReminder(
                toEmail,
                toName,
                referenceNumber,
                "Collection reminder",
                "Your order is still waiting at GymHub.",
                "Please collect it as soon as possible and present the pickup QR shown under My Orders.",
                "#fffbeb",
                "#92400e");
        }

        public static void SendOrderFinalReminder(
            string toEmail,
            string toName,
            string referenceNumber)
        {
            SendCollectionReminder(
                toEmail,
                toName,
                referenceNumber,
                "Final collection reminder",
                "Your collection deadline is approaching.",
                "If the order is not collected by the deadline, it will be marked Uncollected and its items will be released back into stock.",
                "#fef2f2",
                "#991b1b");
        }

        public static void SendOrderUncollected(
            string toEmail,
            string toName,
            string referenceNumber)
        {
            string content =
                Greeting(toName) +
                StatusBanner("Order not collected", "The collection deadline has passed", "#f1f5f9", "#334155") +
                DetailsTable(Row("Order reference", H(referenceNumber)) + Row("Status", "Uncollected")) +
                Paragraph("The reserved items have been released back into available stock. If you still need them, place a new order or contact GymHub support.") +
                Button("Visit GymHub Store", BaseUrl + "/Store");

            SendHtml(
                toEmail,
                toName,
                "Order marked uncollected | " + referenceNumber,
                content,
                "Order uncollected");
        }

        public static void SendReturnRequestReceived(
            string toEmail,
            string toName,
            string orderReference,
            string returnReference,
            string productName,
            int quantity,
            string requestedResolution,
            decimal estimatedCredit,
            string returnQrBase64)
        {
            var builder = new BodyBuilder();
            string qrHtml = string.Empty;

            if (!string.IsNullOrWhiteSpace(returnQrBase64))
            {
                try
                {
                    var qr = builder.LinkedResources.Add(
                        "gymhub-return-qr.png",
                        Convert.FromBase64String(returnQrBase64),
                        ContentType.Parse("image/png"));

                    qr.ContentId = MimeUtils.GenerateMessageId();
                    qr.ContentDisposition = new ContentDisposition(ContentDisposition.Inline);
                    qrHtml = QrPanel(qr.ContentId, returnReference, "Return inspection QR");
                }
                catch (FormatException ex)
                {
                    System.Diagnostics.Debug.WriteLine("[EMAIL QR ERROR] " + ex.Message);
                }
            }

            string content =
                Greeting(toName) +
                StatusBanner("Request received", "Inspection is required", "#eff6ff", "#1d4ed8") +
                Paragraph("We have recorded your return or exchange request. Submitting the request does not automatically approve it.") +
                DetailsTable(
                    Row("Return reference", H(returnReference)) +
                    Row("Order reference", H(orderReference)) +
                    Row("Product", H(productName)) +
                    Row("Quantity", quantity.ToString(CultureInfo.InvariantCulture)) +
                    Row("Requested resolution", H(requestedResolution)) +
                    TotalRow("Estimated return credit", Money(estimatedCredit))) +
                qrHtml +
                Notice("Next step", "Bring the product and show this QR to GymHub Store Staff. Staff will inspect the item, after which an Admin will make the final decision.", "#fffbeb", "#92400e") +
                Button("View request", BaseUrl + "/Store/MyOrders");

            builder.HtmlBody = Wrap(content, "Return request received");
            SendMessage(toEmail, toName, "Return request received | " + returnReference, builder);
        }

        public static void SendReturnInspectionCompleted(
            string toEmail,
            string toName,
            string returnReference,
            string productName,
            string staffRecommendation)
        {
            string content =
                Greeting(toName) +
                StatusBanner("Inspection completed", "Awaiting Admin decision", "#f5f3ff", "#6d28d9") +
                DetailsTable(
                    Row("Return reference", H(returnReference)) +
                    Row("Product", H(productName)) +
                    Row("Staff recommendation", H(staffRecommendation))) +
                Paragraph("The Store Staff recommendation is not the final outcome. An Admin must review the inspection before the request is approved or rejected.") +
                Button("Track my request", BaseUrl + "/Store/MyOrders");

            SendHtml(toEmail, toName, "Return inspected | " + returnReference, content, "Return inspected");
        }

        // Preserves the existing refund-approval controller call.
        public static void SendReturnApproved(
            string toEmail,
            string toName,
            string referenceNumber,
            decimal refundAmount)
        {
            string content =
                Greeting(toName) +
                StatusBanner("Refund approved", "Your return was approved", "#ecfdf5", "#047857") +
                DetailsTable(
                    Row("Return reference", H(referenceNumber)) +
                    TotalRow("Refund amount", Money(refundAmount))) +
                Notice("Refund timing", "The refund is processed to the original payment method. Your bank may take several business days to reflect it.", "#eff6ff", "#1d4ed8") +
                Button("View return status", BaseUrl + "/Store/MyOrders");

            var builder = CreateBuilder(content, "Refund confirmation");
            builder.Attachments.Add(
                SafeFileName("GymHub-Refund-" + referenceNumber + ".html"),
                Encoding.UTF8.GetBytes(BuildRefundStatement(toName, referenceNumber, refundAmount)),
                ContentType.Parse("text/html; charset=utf-8"));

            SendMessage(toEmail, toName, "Refund approved | " + referenceNumber, builder);
        }

        public static void SendReturnRejected(
            string toEmail,
            string toName,
            string referenceNumber,
            string reason)
        {
            string content =
                Greeting(toName) +
                StatusBanner("Request not approved", "Admin review completed", "#fef2f2", "#b91c1c") +
                DetailsTable(Row("Return reference", H(referenceNumber))) +
                Notice("Decision reason", H(reason), "#fff7ed", "#9a3412") +
                Paragraph("If you need clarification, contact GymHub support and quote the return reference above.") +
                Button("View return status", BaseUrl + "/Store/MyOrders");

            SendHtml(toEmail, toName, "Return decision | " + referenceNumber, content, "Return decision");
        }

        public static void SendExchangeApproved(
            string toEmail,
            string toName,
            string returnReference,
            string replacementProduct,
            int replacementQuantity,
            decimal additionalPayment,
            decimal differenceRefund)
        {
            string nextStep = additionalPayment > 0m
                ? "An additional payment is required before the replacement can be collected."
                : "No additional payment is required. We will confirm when the replacement is ready.";

            string content =
                Greeting(toName) +
                StatusBanner("Exchange approved", "Your replacement was authorised", "#ecfdf5", "#047857") +
                DetailsTable(
                    Row("Return reference", H(returnReference)) +
                    Row("Replacement", H(replacementProduct)) +
                    Row("Replacement quantity", replacementQuantity.ToString(CultureInfo.InvariantCulture)) +
                    Row("Additional payment", Money(additionalPayment)) +
                    Row("Difference refund", Money(differenceRefund))) +
                Notice("Next step", nextStep, "#eff6ff", "#1d4ed8") +
                Button("View exchange", BaseUrl + "/Store/MyOrders");

            SendHtml(toEmail, toName, "Exchange approved | " + returnReference, content, "Exchange approved");
        }

        public static void SendExchangePaymentRequired(
            string toEmail,
            string toName,
            string returnReference,
            string replacementProduct,
            decimal amountDue,
            DateTime reservationExpiresAt)
        {
            string content =
                Greeting(toName) +
                StatusBanner("Payment required", "Complete your exchange payment", "#fff7ed", "#c2410c") +
                DetailsTable(
                    Row("Return reference", H(returnReference)) +
                    Row("Replacement", H(replacementProduct)) +
                    TotalRow("Amount due", Money(amountDue)) +
                    Row("Reservation expires", H(reservationExpiresAt.ToString("dd MMM yyyy, HH:mm")))) +
                Notice("Reservation", "The replacement stock is reserved temporarily. Pay from My Orders before the expiry time to keep the reservation.", "#fef2f2", "#b91c1c") +
                Button("Pay exchange difference", BaseUrl + "/Store/MyOrders");

            SendHtml(toEmail, toName, "Exchange payment required | " + returnReference, content, "Exchange payment required");
        }

        public static void SendExchangeReady(
            string toEmail,
            string toName,
            string returnReference,
            string replacementProduct,
            int quantity)
        {
            string content =
                Greeting(toName) +
                StatusBanner("Ready for exchange", "Your replacement is ready", "#ecfdf5", "#047857") +
                DetailsTable(
                    Row("Return reference", H(returnReference)) +
                    Row("Replacement", H(replacementProduct)) +
                    Row("Quantity", quantity.ToString(CultureInfo.InvariantCulture))) +
                Notice("In-store handover", "Bring the approved returned item and your return QR. Store Staff must complete the handover before replacement stock is released.", "#fffbeb", "#92400e") +
                Button("View exchange", BaseUrl + "/Store/MyOrders");

            SendHtml(toEmail, toName, "Exchange ready | " + returnReference, content, "Exchange ready");
        }

        public static void SendExchangeCompleted(
            string toEmail,
            string toName,
            string returnReference,
            string replacementProduct,
            int quantity,
            DateTime completedAt)
        {
            string content =
                Greeting(toName) +
                StatusBanner("Exchange completed", "Replacement handed over", "#ecfdf5", "#047857") +
                DetailsTable(
                    Row("Return reference", H(returnReference)) +
                    Row("Replacement", H(replacementProduct)) +
                    Row("Quantity", quantity.ToString(CultureInfo.InvariantCulture)) +
                    Row("Completed", H(completedAt.ToString("dd MMM yyyy, HH:mm")))) +
                Paragraph("This email is confirmation that the in-store exchange has been completed.") +
                Button("View my orders", BaseUrl + "/Store/MyOrders");

            var builder = CreateBuilder(content, "Exchange receipt");
            builder.Attachments.Add(
                SafeFileName("GymHub-Exchange-" + returnReference + ".html"),
                Encoding.UTF8.GetBytes(BuildExchangeReceipt(toName, returnReference, replacementProduct, quantity, completedAt)),
                ContentType.Parse("text/html; charset=utf-8"));

            SendMessage(toEmail, toName, "Exchange completed | " + returnReference, builder);
        }

        private static void SendCollectionReminder(
            string toEmail,
            string toName,
            string referenceNumber,
            string subjectPrefix,
            string title,
            string instruction,
            string background,
            string colour)
        {
            string content =
                Greeting(toName) +
                StatusBanner(subjectPrefix, title, background, colour) +
                DetailsTable(Row("Order reference", H(referenceNumber))) +
                Paragraph(instruction) +
                Button("Open my orders", BaseUrl + "/Store/MyOrders");

            SendHtml(toEmail, toName, subjectPrefix + " | " + referenceNumber, content, subjectPrefix);
        }

        private static BodyBuilder CreateBuilder(string content, string preheader)
        {
            return new BodyBuilder { HtmlBody = Wrap(content, preheader) };
        }

        private static void SendHtml(
            string toEmail,
            string toName,
            string subject,
            string content,
            string preheader)
        {
            SendMessage(toEmail, toName, subject, CreateBuilder(content, preheader));
        }

        private static void SendMessage(
            string toEmail,
            string toName,
            string subject,
            BodyBuilder builder)
        {
            if (string.IsNullOrWhiteSpace(toEmail))
                throw new ArgumentException("A recipient email address is required.", nameof(toEmail));

            if (string.IsNullOrWhiteSpace(SenderEmail) || string.IsNullOrWhiteSpace(AppPassword))
                throw new InvalidOperationException("GymHub SMTP settings are missing from Web.config.");

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(SenderName, SenderEmail));
            message.To.Add(new MailboxAddress(toName ?? "GymHub Member", toEmail));
            message.Subject = subject;
            message.Body = builder.ToMessageBody();

            using (var client = new SmtpClient())
            {
                client.Timeout = 30000;
                client.Connect(SmtpHost, SmtpPort, SecureSocketOptions.StartTls);
                client.Authenticate(SenderEmail, AppPassword);
                client.Send(message);
                client.Disconnect(true);
            }
        }

        private static string Wrap(string content, string preheader)
        {
            return @"<!doctype html><html><head><meta charset='utf-8'><meta name='viewport' content='width=device-width,initial-scale=1'></head>" +
                "<body style='margin:0;padding:0;background:#eef2f7;font-family:Arial,Helvetica,sans-serif;color:#0f172a;'>" +
                "<div style='display:none;max-height:0;overflow:hidden;opacity:0;color:transparent;'>" + H(preheader) + "</div>" +
                "<table role='presentation' width='100%' cellspacing='0' cellpadding='0' style='background:#eef2f7;padding:28px 12px;'>" +
                "<tr><td align='center'><table role='presentation' width='100%' cellspacing='0' cellpadding='0' style='max-width:640px;background:#ffffff;border:1px solid #dbe3ee;border-radius:16px;overflow:hidden;'>" +
                "<tr><td style='padding:26px 30px;background:#0f172a;color:#ffffff;'>" +
                "<div style='font-size:27px;font-weight:900;letter-spacing:3px;'>GYM<span style='color:#3b82f6;'>HUB</span></div>" +
                "<div style='margin-top:5px;color:#94a3b8;font-size:11px;letter-spacing:1px;text-transform:uppercase;'>Store Services</div></td></tr>" +
                "<tr><td style='padding:30px;'>" + content + "</td></tr>" +
                "<tr><td style='padding:21px 30px;background:#f8fafc;border-top:1px solid #e2e8f0;text-align:center;color:#64748b;font-size:11px;line-height:1.6;'>" +
                "&copy; " + DateTime.Now.Year + " GymHub (Pty) Ltd &nbsp;|&nbsp; " + H(SupportEmail) +
                "<br>This is an automated service email. Keep all reference numbers for your records.</td></tr>" +
                "</table></td></tr></table></body></html>";
        }

        private static string Greeting(string name)
        {
            return "<p style='margin:0 0 16px;color:#334155;font-size:15px;line-height:1.7;'>Hello <strong>" +
                H(string.IsNullOrWhiteSpace(name) ? "Member" : name) + "</strong>,</p>";
        }

        private static string Paragraph(string text)
        {
            return "<p style='margin:0 0 18px;color:#475569;font-size:14px;line-height:1.7;'>" + H(text) + "</p>";
        }

        private static string StatusBanner(string eyebrow, string title, string background, string colour)
        {
            return "<div style='margin:0 0 20px;padding:18px;border-radius:11px;background:" + background + ";color:" + colour + ";'>" +
                "<div style='font-size:10px;font-weight:800;letter-spacing:1px;text-transform:uppercase;'>" + H(eyebrow) + "</div>" +
                "<div style='margin-top:5px;font-size:20px;font-weight:800;'>" + H(title) + "</div></div>";
        }

        private static string DetailsTable(string rows)
        {
            return "<table role='presentation' width='100%' cellspacing='0' cellpadding='0' style='margin:0 0 20px;border:1px solid #e2e8f0;border-radius:10px;border-collapse:separate;overflow:hidden;'>" + rows + "</table>";
        }

        private static string Row(string label, string value, string valueColour = "#0f172a")
        {
            return "<tr><td style='padding:10px 13px;border-bottom:1px solid #f1f5f9;color:#64748b;font-size:12px;'>" + H(label) +
                "</td><td align='right' style='padding:10px 13px;border-bottom:1px solid #f1f5f9;color:" + valueColour + ";font-size:12px;font-weight:700;'>" + value + "</td></tr>";
        }

        private static string TotalRow(string label, string value)
        {
            return "<tr><td style='padding:13px;background:#f8fafc;color:#0f172a;font-size:13px;font-weight:800;'>" + H(label) +
                "</td><td align='right' style='padding:13px;background:#f8fafc;color:#0f172a;font-size:16px;font-weight:900;'>" + value + "</td></tr>";
        }

        private static string Notice(string title, string text, string background, string colour)
        {
            return "<div style='margin:0 0 20px;padding:14px 16px;border-radius:9px;background:" + background + ";color:" + colour + ";font-size:12px;line-height:1.6;'>" +
                "<strong>" + H(title) + ":</strong> " + text + "</div>";
        }

        private static string Button(string text, string url)
        {
            return "<div style='margin:23px 0 5px;text-align:center;'><a href='" + H(url) +
                "' style='display:inline-block;padding:12px 22px;border-radius:9px;background:#2563eb;color:#ffffff;text-decoration:none;font-size:13px;font-weight:800;'>" + H(text) + "</a></div>";
        }

        private static string QrPanel(string contentId, string reference, string title)
        {
            return "<div style='margin:20px 0;padding:20px;border:2px dashed #bfdbfe;border-radius:12px;background:#f8fbff;text-align:center;'>" +
                "<div style='margin-bottom:13px;color:#0f172a;font-size:13px;font-weight:800;'>" + H(title) + "</div>" +
                "<img src='cid:" + H(contentId) + "' width='210' height='210' alt='GymHub QR code' style='display:block;margin:0 auto;width:210px;height:210px;border:8px solid #ffffff;border-radius:10px;'>" +
                "<div style='margin-top:11px;color:#475569;font-size:12px;font-weight:800;letter-spacing:.7px;'>" + H(reference) + "</div></div>";
        }

        private static string BuildInvoiceHtml(
            string customerName,
            string reference,
            DateTime paidAt,
            string paymentReference,
            decimal subtotal,
            decimal discount,
            decimal total,
            IEnumerable<StoreEmailLineItem> items)
        {
            string rows = string.Join("", items.Select(i =>
                "<tr><td>" + H(i.ProductName) + "</td><td>" + i.Quantity + "</td><td>" + Money(i.UnitPrice) + "</td><td>" + Money(i.LineTotal) + "</td></tr>"));

            return DocumentShell(
                "TAX INVOICE / RECEIPT",
                "<p><strong>Order:</strong> " + H(reference) + "<br><strong>Customer:</strong> " + H(customerName) +
                "<br><strong>Paid:</strong> " + H(paidAt.ToString("dd MMM yyyy, HH:mm")) +
                (!string.IsNullOrWhiteSpace(paymentReference) ? "<br><strong>Payment reference:</strong> " + H(paymentReference) : "") + "</p>" +
                "<table><thead><tr><th>Product</th><th>Qty</th><th>Unit price</th><th>Total</th></tr></thead><tbody>" + rows + "</tbody></table>" +
                "<div class='totals'><p>Subtotal: " + Money(subtotal) + "</p><p>Discount: -" + Money(discount) + "</p><h2>Total paid: " + Money(total) + "</h2></div>");
        }

        private static string BuildRefundStatement(string customerName, string reference, decimal amount)
        {
            return DocumentShell(
                "REFUND CONFIRMATION",
                "<p><strong>Member:</strong> " + H(customerName) + "<br><strong>Return reference:</strong> " + H(reference) +
                "<br><strong>Decision date:</strong> " + H(DateTime.Now.ToString("dd MMM yyyy, HH:mm")) +
                "</p><div class='totals'><h2>Approved refund: " + Money(amount) + "</h2></div>" +
                "<p>The amount will be returned to the original payment method, subject to bank processing times.</p>");
        }

        private static string BuildExchangeReceipt(string customerName, string reference, string product, int quantity, DateTime completedAt)
        {
            return DocumentShell(
                "EXCHANGE RECEIPT",
                "<p><strong>Member:</strong> " + H(customerName) + "<br><strong>Return reference:</strong> " + H(reference) +
                "<br><strong>Completed:</strong> " + H(completedAt.ToString("dd MMM yyyy, HH:mm")) +
                "</p><table><thead><tr><th>Replacement product</th><th>Quantity</th></tr></thead><tbody><tr><td>" +
                H(product) + "</td><td>" + quantity + "</td></tr></tbody></table>");
        }

        private static string DocumentShell(string title, string body)
        {
            return "<!doctype html><html><head><meta charset='utf-8'><title>" + H(title) +
                "</title><style>body{font-family:Arial,sans-serif;color:#0f172a;margin:35px}header{background:#0f172a;color:white;padding:22px}header span{color:#3b82f6}h1{font-size:23px}table{width:100%;border-collapse:collapse;margin-top:22px}th,td{border:1px solid #dbe3ee;padding:10px;text-align:left}th{background:#f1f5f9}.totals{text-align:right;margin-top:20px}footer{margin-top:35px;padding-top:15px;border-top:1px solid #e2e8f0;color:#64748b;font-size:11px}@media print{body{margin:15mm}}</style></head><body>" +
                "<header><strong style='font-size:25px;letter-spacing:2px'>GYM<span>HUB</span></strong><div>Store Services</div></header><h1>" + H(title) + "</h1>" + body +
                "<footer>Generated by GymHub on " + H(DateTime.Now.ToString("dd MMM yyyy, HH:mm")) + " &middot; " + H(SupportEmail) + "</footer></body></html>";
        }

        private static string Money(decimal amount)
        {
            return "R" + amount.ToString("N2", CultureInfo.GetCultureInfo("en-ZA"));
        }

        private static string H(string value)
        {
            return WebUtility.HtmlEncode(value ?? string.Empty);
        }

        private static string SafeFileName(string value)
        {
            var invalid = System.IO.Path.GetInvalidFileNameChars();
            return new string((value ?? "GymHub-Document.html").Select(c => invalid.Contains(c) ? '-' : c).ToArray());
        }

        private static string Setting(string key, string fallback)
        {
            string value = WebConfigurationManager.AppSettings[key];
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }
    }
}
