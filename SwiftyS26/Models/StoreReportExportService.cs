using iTextSharp.text;
using iTextSharp.text.pdf;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using SwiftyS26.Models;
using SwiftyS26.ViewModels;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;

// Explicit aliases prevent conflicts with System.Windows.Documents.
using PdfDocument = iTextSharp.text.Document;
using PdfParagraph = iTextSharp.text.Paragraph;
using PdfWriterAlias = iTextSharp.text.pdf.PdfWriter;

namespace SwiftyS26.Services
{
    public static class StoreReportExportService
    {
        private static readonly BaseColor Blue =
            new BaseColor(37, 99, 235);

        private static readonly BaseColor Navy =
            new BaseColor(15, 23, 42);

        private static readonly BaseColor LightBorder =
            new BaseColor(226, 232, 240);

        // ============================================================
        // ORDER HISTORY PDF
        // ============================================================

        public static byte[] OrderHistoryPdf(
            IList<StoreOrder> orders,
            string generatedBy)
        {
            if (orders == null)
            {
                orders = new List<StoreOrder>();
            }

            return CreateSummaryPdf(
                "STORE ORDER HISTORY",
                generatedBy,
                new[]
                {
                    "Order",
                    "Member",
                    "Created",
                    "Subtotal",
                    "Discount",
                    "Amount Paid",
                    "Status"
                },
                orders.Select(order => new[]
                {
                    SafeText(order.ReferenceNumber),

                    order.Member != null
                        ? SafeText(order.Member.FullName)
                        : "Unknown member",

                    order.CreatedAt.ToString(
                        "dd MMM yyyy HH:mm"),

                    FormatMoney(order.Subtotal),

                    FormatMoney(order.DiscountAmount),

                    FormatMoney(order.FinalTotal),

                    FormatStatus(order.Status)
                }));
        }

        // ============================================================
        // COLLECTION HISTORY PDF
        // ============================================================

        public static byte[] CollectionHistoryPdf(
            IList<StoreOrder> orders,
            string generatedBy)
        {
            if (orders == null)
            {
                orders = new List<StoreOrder>();
            }

            return CreateSummaryPdf(
                "COLLECTION HISTORY",
                generatedBy,
                new[]
                {
                    "Order",
                    "Member",
                    "Ready Date",
                    "Deadline",
                    "Collected Date",
                    "Outcome",
                    "Handled By"
                },
                orders.Select(order => new[]
                {
                    SafeText(order.ReferenceNumber),

                    order.Member != null
                        ? SafeText(order.Member.FullName)
                        : "Unknown member",

                    FormatDate(order.ReadyAt),

                    FormatDate(order.PickupDeadline),

                    FormatDate(order.CollectedAt),

                    StoreReportService.GetCollectionOutcome(
                        order),

                    order.CollectedByStaffId.HasValue
                        ? "Store Staff #" +
                          order.CollectedByStaffId.Value
                        : "—"
                }));
        }

        // ============================================================
        // INVENTORY MOVEMENT HISTORY PDF
        // ============================================================

        public static byte[] InventoryHistoryPdf(
            IList<InventoryMovement> movements,
            string generatedBy)
        {
            if (movements == null)
            {
                movements =
                    new List<InventoryMovement>();
            }

            return CreateSummaryPdf(
                "INVENTORY MOVEMENT HISTORY",
                generatedBy,
                new[]
                {
                    "Date",
                    "Product",
                    "Change",
                    "Movement Type",
                    "Reference",
                    "Performed By",
                    "Notes"
                },
                movements.Select(movement => new[]
                {
                    movement.CreatedAt.ToString(
                        "dd MMM yyyy HH:mm"),

                    movement.Product != null
                        ? SafeText(movement.Product.Name)
                        : "Unknown product",

                    FormatSignedQuantity(
                        movement.QuantityChange),

                    FormatMovementType(
                        movement.MovementType),

                    string.IsNullOrWhiteSpace(
                        movement.ReferenceNumber)
                            ? "—"
                            : movement.ReferenceNumber,

                    string.IsNullOrWhiteSpace(
                        movement.PerformedBy)
                            ? "GymHub System"
                            : movement.PerformedBy,

                    string.IsNullOrWhiteSpace(
                        movement.Notes)
                            ? "—"
                            : movement.Notes
                }));
        }

        // ============================================================
        // ORDER HISTORY EXCEL
        // ============================================================

        public static byte[] OrderHistoryExcel(
            IList<StoreOrder> orders,
            string generatedBy)
        {
            if (orders == null)
            {
                orders = new List<StoreOrder>();
            }

            return CreateExcelWorkbook(
                "Order History",
                "GYMHUB STORE ORDER HISTORY",
                generatedBy,
                new[]
                {
                    "Order",
                    "Member",
                    "Email",
                    "Created",
                    "Item Quantity",
                    "Subtotal",
                    "Discount",
                    "Amount Paid",
                    "Paystack Reference",
                    "Status"
                },
                orders.Select(order => new object[]
                {
                    order.ReferenceNumber,

                    order.Member != null
                        ? order.Member.FullName
                        : "Unknown member",

                    order.Member != null
                        ? order.Member.Email
                        : "",

                    order.CreatedAt,

                    order.Items != null
                        ? order.Items.Sum(item =>
                            item.Quantity)
                        : 0,

                    order.Subtotal,

                    order.DiscountAmount,

                    order.FinalTotal,

                    order.PaystackReference,

                    FormatStatus(order.Status)
                }),
                new[] { 6, 7, 8 },
                new[] { 4 });
        }

        // ============================================================
        // COLLECTION HISTORY EXCEL
        // ============================================================

        public static byte[] CollectionHistoryExcel(
            IList<StoreOrder> orders,
            string generatedBy)
        {
            if (orders == null)
            {
                orders = new List<StoreOrder>();
            }

            return CreateExcelWorkbook(
                "Collection History",
                "GYMHUB COLLECTION HISTORY",
                generatedBy,
                new[]
                {
                    "Order",
                    "Member",
                    "Email",
                    "Ready Date",
                    "Deadline",
                    "Collected Date",
                    "Outcome",
                    "Collected By",
                    "Signature"
                },
                orders.Select(order => new object[]
                {
                    order.ReferenceNumber,

                    order.Member != null
                        ? order.Member.FullName
                        : "Unknown member",

                    order.Member != null
                        ? order.Member.Email
                        : "",

                    order.ReadyAt,

                    order.PickupDeadline,

                    order.CollectedAt,

                    StoreReportService.GetCollectionOutcome(
                        order),

                    order.CollectedByStaffId.HasValue
                        ? "Store Staff #" +
                          order.CollectedByStaffId.Value
                        : "",

                    string.IsNullOrWhiteSpace(
                        order.PickupSignaturePath)
                            ? "Not captured"
                            : "Captured"
                }),
                new int[0],
                new[] { 4, 5, 6 });
        }

        // ============================================================
        // INVENTORY MOVEMENT HISTORY EXCEL
        // ============================================================

        public static byte[] InventoryHistoryExcel(
            IList<InventoryMovement> movements,
            string generatedBy)
        {
            if (movements == null)
            {
                movements =
                    new List<InventoryMovement>();
            }

            return CreateExcelWorkbook(
                "Inventory History",
                "GYMHUB INVENTORY MOVEMENT HISTORY",
                generatedBy,
                new[]
                {
                    "Date",
                    "Product",
                    "Product ID",
                    "Quantity Change",
                    "Movement Type",
                    "Reference Type",
                    "Reference",
                    "Performed By",
                    "Notes"
                },
                movements.Select(movement => new object[]
                {
                    movement.CreatedAt,

                    movement.Product != null
                        ? movement.Product.Name
                        : "Unknown product",

                    movement.ProductId,

                    movement.QuantityChange,

                    FormatMovementType(
                        movement.MovementType),

                    movement.ReferenceType,

                    movement.ReferenceNumber,

                    string.IsNullOrWhiteSpace(
                        movement.PerformedBy)
                            ? "GymHub System"
                            : movement.PerformedBy,

                    movement.Notes
                }),
                new int[0],
                new[] { 1 });
        }

        // ============================================================
        // INDIVIDUAL ORDER PDF
        // ============================================================

        public static byte[] OrderRecordPdf(
            OrderHistoryDetailsViewModel model,
            string generatedBy)
        {
            if (model == null ||
                model.Order == null)
            {
                throw new ArgumentException(
                    "A valid order record is required.",
                    nameof(model));
            }

            StoreOrder order = model.Order;

            using (var stream = new MemoryStream())
            {
                PdfWriterAlias writer;

                PdfDocument document =
                    CreateDocument(
                        stream,
                        out writer);

                AddBrandHeader(
                    document,
                    "FORMAL STORE ORDER RECORD",
                    generatedBy);

                AddSectionHeading(
                    document,
                    "ORDER AND MEMBER INFORMATION");

                AddKeyValueTable(
                    document,
                    new[]
                    {
                        Pair(
                            "Order reference",
                            order.ReferenceNumber),

                        Pair(
                            "Order status",
                            FormatStatus(order.Status)),

                        Pair(
                            "Member",
                            order.Member != null
                                ? order.Member.FullName
                                : "Unknown member"),

                        Pair(
                            "Member email",
                            order.Member != null
                                ? order.Member.Email
                                : "—"),

                        Pair(
                            "Created date",
                            order.CreatedAt.ToString(
                                "dd MMM yyyy HH:mm")),

                        Pair(
                            "Paid date",
                            FormatDate(order.PaidAt)),

                        Pair(
                            "Paystack reference",
                            order.PaystackReference)
                    });

                AddSectionHeading(
                    document,
                    "ORDERED ITEMS");

                IEnumerable<string[]> itemRows =
                    order.Items != null
                        ? order.Items.Select(item =>
                            new[]
                            {
                                item.Product != null
                                    ? item.Product.Name
                                    : "Unknown product",

                                item.Quantity.ToString(),

                                FormatMoney(
                                    item.LineTotal)
                            })
                        : Enumerable.Empty<string[]>();

                AddPdfTable(
                    document,
                    new[]
                    {
                        "Product",
                        "Quantity",
                        "Line Total"
                    },
                    itemRows);

                AddSectionHeading(
                    document,
                    "FINANCIAL SUMMARY");

                AddKeyValueTable(
                    document,
                    new[]
                    {
                        Pair(
                            "Subtotal",
                            FormatMoney(order.Subtotal)),

                        Pair(
                            "Premium discount",
                            "-" +
                            FormatMoney(
                                order.DiscountAmount)),

                        Pair(
                            "Amount paid",
                            FormatMoney(order.FinalTotal))
                    });

                AddSectionHeading(
                    document,
                    "COLLECTION INFORMATION");

                AddKeyValueTable(
                    document,
                    new[]
                    {
                        Pair(
                            "Ready date",
                            FormatDate(order.ReadyAt)),

                        Pair(
                            "Collection deadline",
                            FormatDate(
                                order.PickupDeadline)),

                        Pair(
                            "Collected date",
                            FormatDate(
                                order.CollectedAt)),

                        Pair(
                            "Collected by",
                            order.CollectedByStaffId
                                .HasValue
                                    ? "Store Staff #" +
                                      order
                                          .CollectedByStaffId
                                          .Value
                                    : "—"),

                        Pair(
                            "Pickup signature",
                            string.IsNullOrWhiteSpace(
                                order.PickupSignaturePath)
                                    ? "Not captured"
                                    : "Captured")
                    });

                if (model.CollectionEvents != null &&
                    model.CollectionEvents.Any())
                {
                    AddSectionHeading(
                        document,
                        "COLLECTION AUDIT TIMELINE");

                    AddPdfTable(
                        document,
                        new[]
                        {
                            "Date",
                            "Event",
                            "Description",
                            "Performed By"
                        },
                        model.CollectionEvents.Select(
                            auditEvent => new[]
                            {
                                auditEvent.CreatedAt
                                    .ToString(
                                        "dd MMM yyyy HH:mm"),

                                auditEvent.EventType,

                                auditEvent.Description,

                                string.IsNullOrWhiteSpace(
                                    auditEvent.PerformedBy)
                                        ? "GymHub System"
                                        : auditEvent.PerformedBy
                            }));
                }

                document.Close();

                return stream.ToArray();
            }
        }

        // ============================================================
        // INDIVIDUAL COLLECTION PDF
        // ============================================================

        public static byte[] CollectionRecordPdf(
            CollectionHistoryDetailsViewModel model,
            string generatedBy)
        {
            if (model == null ||
                model.Order == null)
            {
                throw new ArgumentException(
                    "A valid collection record is required.",
                    nameof(model));
            }

            StoreOrder order = model.Order;

            using (var stream = new MemoryStream())
            {
                PdfWriterAlias writer;

                PdfDocument document =
                    CreateDocument(
                        stream,
                        out writer);

                AddBrandHeader(
                    document,
                    "FORMAL COLLECTION RECORD",
                    generatedBy);

                AddSectionHeading(
                    document,
                    "COLLECTION SUMMARY");

                AddKeyValueTable(
                    document,
                    new[]
                    {
                        Pair(
                            "Order reference",
                            order.ReferenceNumber),

                        Pair(
                            "Member",
                            order.Member != null
                                ? order.Member.FullName
                                : "Unknown member"),

                        Pair(
                            "Member email",
                            order.Member != null
                                ? order.Member.Email
                                : "—"),

                        Pair(
                            "Outcome",
                            model.CollectionOutcome),

                        Pair(
                            "Ready date",
                            FormatDate(order.ReadyAt)),

                        Pair(
                            "Collection deadline",
                            FormatDate(
                                order.PickupDeadline)),

                        Pair(
                            "Collected date",
                            FormatDate(
                                order.CollectedAt)),

                        Pair(
                            "Collected by",
                            order.CollectedByStaffId
                                .HasValue
                                    ? "Store Staff #" +
                                      order
                                          .CollectedByStaffId
                                          .Value
                                    : "—"),

                        Pair(
                            "Pickup signature",
                            string.IsNullOrWhiteSpace(
                                order.PickupSignaturePath)
                                    ? "Not captured"
                                    : "Captured"),

                        Pair(
                            "Collected on time",
                            model.WasCollectedOnTime
                                ? "Yes"
                                : "No"),

                        Pair(
                            "Deadline extended",
                            model.WasDeadlineExtended
                                ? "Yes"
                                : "No"),

                        Pair(
                            "Collection duration",
                            FormatDuration(
                                model.CollectionDuration))
                    });

                AddSectionHeading(
                    document,
                    "COLLECTION AUDIT TIMELINE");

                IEnumerable<string[]> eventRows =
                    model.Events != null
                        ? model.Events.Select(
                            auditEvent => new[]
                            {
                                auditEvent.CreatedAt.ToString(
                                    "dd MMM yyyy HH:mm"),

                                auditEvent.EventType,

                                auditEvent.Description,

                                string.IsNullOrWhiteSpace(
                                    auditEvent.PerformedBy)
                                        ? "GymHub System"
                                        : auditEvent.PerformedBy
                            })
                        : Enumerable.Empty<string[]>();

                AddPdfTable(
                    document,
                    new[]
                    {
                        "Date",
                        "Event",
                        "Description",
                        "Performed By"
                    },
                    eventRows);

                document.Close();

                return stream.ToArray();
            }
        }

        // ============================================================
        // GENERIC PDF CREATOR
        // ============================================================

        private static byte[] CreateSummaryPdf(
            string title,
            string generatedBy,
            string[] headers,
            IEnumerable<string[]> rows)
        {
            using (var stream = new MemoryStream())
            {
                PdfWriterAlias writer;

                PdfDocument document =
                    CreateDocument(
                        stream,
                        out writer);

                AddBrandHeader(
                    document,
                    title,
                    generatedBy);

                AddPdfTable(
                    document,
                    headers,
                    rows);

                document.Close();

                return stream.ToArray();
            }
        }

        private static PdfDocument CreateDocument(
            Stream stream,
            out PdfWriterAlias writer)
        {
            var document = new PdfDocument(
                PageSize.A4.Rotate(),
                28,
                28,
                30,
                30);

            writer =
                PdfWriterAlias.GetInstance(
                    document,
                    stream);

            document.Open();

            return document;
        }

        private static void AddBrandHeader(
            PdfDocument document,
            string title,
            string generatedBy)
        {
            var brand = new PdfParagraph(
                "GYMHUB",
                FontFactory.GetFont(
                    FontFactory.HELVETICA_BOLD,
                    22,
                    Blue));

            brand.SpacingAfter = 2;
            document.Add(brand);

            var heading = new PdfParagraph(
                title,
                FontFactory.GetFont(
                    FontFactory.HELVETICA_BOLD,
                    15,
                    Navy));

            heading.SpacingAfter = 4;
            document.Add(heading);

            var metadata = new PdfParagraph(
                "Generated " +
                DateTime.Now.ToString(
                    "dd MMM yyyy HH:mm") +
                " by " +
                SafeText(generatedBy),
                FontFactory.GetFont(
                    FontFactory.HELVETICA,
                    8,
                    BaseColor.GRAY));

            metadata.SpacingAfter = 14;
            document.Add(metadata);
        }

        private static void AddSectionHeading(
            PdfDocument document,
            string title)
        {
            var paragraph = new PdfParagraph(
                title,
                FontFactory.GetFont(
                    FontFactory.HELVETICA_BOLD,
                    11,
                    Blue));

            paragraph.SpacingBefore = 12;
            paragraph.SpacingAfter = 6;

            document.Add(paragraph);
        }

        private static void AddKeyValueTable(
            PdfDocument document,
            IEnumerable<KeyValuePair<string, string>>
                values)
        {
            var table = new PdfPTable(2)
            {
                WidthPercentage = 100
            };

            foreach (var item in values)
            {
                AddPdfCell(
                    table,
                    item.Key,
                    true);

                AddPdfCell(
                    table,
                    item.Value,
                    false);
            }

            document.Add(table);
        }

        private static void AddPdfTable(
            PdfDocument document,
            string[] headers,
            IEnumerable<string[]> rows)
        {
            var table =
                new PdfPTable(headers.Length)
                {
                    WidthPercentage = 100,
                    HeaderRows = 1
                };

            foreach (string header in headers)
            {
                AddPdfCell(
                    table,
                    header,
                    true);
            }

            if (rows != null)
            {
                foreach (string[] row in rows)
                {
                    for (int column = 0;
                         column < headers.Length;
                         column++)
                    {
                        string value =
                            row != null &&
                            column < row.Length
                                ? row[column]
                                : "—";

                        AddPdfCell(
                            table,
                            value,
                            false);
                    }
                }
            }

            document.Add(table);
        }

        private static void AddPdfCell(
            PdfPTable table,
            string value,
            bool isHeader)
        {
            var font = FontFactory.GetFont(
                FontFactory.HELVETICA,
                isHeader ? 8 : 7,
                isHeader
                    ? BaseColor.WHITE
                    : Navy);

            var cell = new PdfPCell(
                new Phrase(
                    SafeText(value),
                    font))
            {
                BackgroundColor =
                    isHeader
                        ? Blue
                        : BaseColor.WHITE,

                Padding = 6,

                BorderColor =
                    isHeader
                        ? Blue
                        : LightBorder,

                VerticalAlignment =
                    Element.ALIGN_MIDDLE
            };

            table.AddCell(cell);
        }

        // ============================================================
        // GENERIC EXCEL CREATOR
        // ============================================================

        private static byte[] CreateExcelWorkbook(
            string worksheetName,
            string title,
            string generatedBy,
            string[] headers,
            IEnumerable<object[]> rows,
            int[] moneyColumns,
            int[] dateColumns)
        {
            using (var package = new ExcelPackage())
            {
                ExcelWorksheet worksheet =
                    package.Workbook.Worksheets.Add(
                        worksheetName);

                worksheet.Cells[
                    1,
                    1,
                    1,
                    headers.Length].Merge = true;

                worksheet.Cells[1, 1].Value =
                    title;

                worksheet.Cells[1, 1].Style.Font.Bold =
                    true;

                worksheet.Cells[1, 1].Style.Font.Size =
                    16;

                worksheet.Cells[1, 1]
                    .Style.Font.Color.SetColor(
                        Color.White);

                worksheet.Cells[1, 1]
                    .Style.Fill.PatternType =
                    ExcelFillStyle.Solid;

                worksheet.Cells[1, 1]
                    .Style.Fill.BackgroundColor
                    .SetColor(
                        Color.FromArgb(
                            37,
                            99,
                            235));

                worksheet.Cells[
                    2,
                    1,
                    2,
                    headers.Length].Merge = true;

                worksheet.Cells[2, 1].Value =
                    "Generated " +
                    DateTime.Now.ToString(
                        "dd MMM yyyy HH:mm") +
                    " by " +
                    SafeText(generatedBy);

                for (int column = 0;
                     column < headers.Length;
                     column++)
                {
                    worksheet.Cells[
                        4,
                        column + 1].Value =
                        headers[column];
                }

                using (ExcelRange headerRange =
                    worksheet.Cells[
                        4,
                        1,
                        4,
                        headers.Length])
                {
                    headerRange.Style.Font.Bold =
                        true;

                    headerRange.Style.Fill.PatternType =
                        ExcelFillStyle.Solid;

                    headerRange.Style.Fill
                        .BackgroundColor.SetColor(
                            Color.FromArgb(
                                226,
                                232,
                                240));

                    headerRange.Style.Border
                        .Bottom.Style =
                        ExcelBorderStyle.Thin;
                }

                int currentRow = 5;

                if (rows != null)
                {
                    foreach (object[] row in rows)
                    {
                        for (int column = 0;
                             column < headers.Length;
                             column++)
                        {
                            object value =
                                row != null &&
                                column < row.Length
                                    ? row[column]
                                    : null;

                            worksheet.Cells[
                                currentRow,
                                column + 1].Value =
                                value;
                        }

                        currentRow++;
                    }
                }

                if (moneyColumns != null)
                {
                    foreach (int column in
                        moneyColumns)
                    {
                        worksheet.Column(column)
                            .Style.Numberformat.Format =
                            "\"R\"#,##0.00";
                    }
                }

                if (dateColumns != null)
                {
                    foreach (int column in
                        dateColumns)
                    {
                        worksheet.Column(column)
                            .Style.Numberformat.Format =
                            "dd mmm yyyy hh:mm";
                    }
                }

                if (currentRow > 5)
                {
                    worksheet.Cells[
                        4,
                        1,
                        currentRow - 1,
                        headers.Length]
                        .AutoFilter = true;
                }

                worksheet.View.FreezePanes(
                    5,
                    1);

                worksheet.Cells[
                    worksheet.Dimension.Address]
                    .AutoFitColumns(10, 45);

                return package.GetAsByteArray();
            }
        }

        // ============================================================
        // FORMATTING HELPERS
        // ============================================================

        private static KeyValuePair<string, string>
            Pair(
                string key,
                string value)
        {
            return new KeyValuePair<string, string>(
                key,
                string.IsNullOrWhiteSpace(value)
                    ? "—"
                    : value);
        }

        private static string FormatMoney(
            decimal amount)
        {
            return "R" + amount.ToString("N2");
        }

        private static string FormatSignedQuantity(
            int quantity)
        {
            return quantity > 0
                ? "+" + quantity
                : quantity.ToString();
        }

        private static string FormatDate(
            DateTime? value)
        {
            return value.HasValue
                ? value.Value.ToString(
                    "dd MMM yyyy HH:mm")
                : "—";
        }

        private static string FormatDuration(
            TimeSpan? duration)
        {
            if (!duration.HasValue)
            {
                return "Not available";
            }

            TimeSpan value = duration.Value;

            if (value.TotalDays >= 1)
            {
                return
                    ((int)value.TotalDays) +
                    " day(s), " +
                    value.Hours +
                    " hour(s), " +
                    value.Minutes +
                    " minute(s)";
            }

            return
                ((int)value.TotalHours) +
                " hour(s), " +
                value.Minutes +
                " minute(s)";
        }

        private static string SafeText(
            string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? "—"
                : value.Trim();
        }

        private static string FormatMovementType(
            string movementType)
        {
            switch (movementType)
            {
                case "SupplierDelivery":
                    return "Supplier Delivery";

                case "StoreOrderReservation":
                    return "Store Order Reservation";

                case "UncollectedRelease":
                    return "Uncollected Stock Release";

                case "ReturnRestocked":
                    return "Member Return Restocked";

                case "ExchangeReturned":
                    return "Exchange Item Returned";

                case "ExchangeIssued":
                    return "Exchange Product Issued";

                case "DamagedWriteOff":
                    return "Damaged Stock Write-Off";

                case "SupplierReturn":
                    return "Return to Supplier";

                case "ManualAdjustment":
                    return "Manual Adjustment";

                case "InitialStock":
                    return "Initial Stock";

                default:
                    return SafeText(movementType);
            }
        }

        private static string FormatStatus(
            string status)
        {
            switch (status)
            {
                case "ReadyForPickup":
                    return "Ready for Pickup";

                case "UncollectedPendingReview":
                    return "Uncollected – Awaiting Review";

                case "UncollectedRefundPending":
                    return "Uncollected – Refund Pending";

                case "UncollectedRefunded":
                    return "Uncollected – Refunded";

                case "UncollectedResolved":
                    return "Uncollected – Resolved";

                default:
                    return SafeText(status);
            }
        }
    }
}