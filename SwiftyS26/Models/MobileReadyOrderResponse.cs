using System;
using System.Collections.Generic;

using Newtonsoft.Json;

namespace SwiftyS26.Models
{
    public class MobileReadyOrderResponse
    {
        [JsonProperty("success")]
        public bool Success { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("orderId")]
        public int OrderId { get; set; }

        [JsonProperty("referenceNumber")]
        public string ReferenceNumber { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("createdAt")]
        public DateTime CreatedAt { get; set; }

        [JsonProperty("paidAt")]
        public DateTime? PaidAt { get; set; }

        [JsonProperty("subtotal")]
        public decimal Subtotal { get; set; }

        [JsonProperty("discountAmount")]
        public decimal DiscountAmount { get; set; }

        [JsonProperty("finalTotal")]
        public decimal FinalTotal { get; set; }

        [JsonProperty("member")]
        public MobileReadyMemberDto Member { get; set; }

        [JsonProperty("items")]
        public List<MobileReadyOrderItemDto> Items { get; set; }
    }

    public class MobileReadyMemberDto
    {
        [JsonProperty("memberId")]
        public int MemberId { get; set; }

        [JsonProperty("memberNumber")]
        public string MemberNumber { get; set; }

        [JsonProperty("fullName")]
        public string FullName { get; set; }

        [JsonProperty("email")]
        public string Email { get; set; }

        [JsonProperty("phone")]
        public string Phone { get; set; }

        [JsonProperty("plan")]
        public string Plan { get; set; }
    }

    public class MobileReadyOrderItemDto
    {
        [JsonProperty("storeOrderItemId")]
        public int StoreOrderItemId { get; set; }

        [JsonProperty("productId")]
        public int ProductId { get; set; }

        [JsonProperty("productName")]
        public string ProductName { get; set; }

        [JsonProperty("category")]
        public string Category { get; set; }

        [JsonProperty("imagePath")]
        public string ImagePath { get; set; }

        [JsonProperty("quantity")]
        public int Quantity { get; set; }

        [JsonProperty("unitPrice")]
        public decimal UnitPrice { get; set; }

        [JsonProperty("lineTotal")]
        public decimal LineTotal { get; set; }
    }
}