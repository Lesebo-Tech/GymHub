using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;


namespace SwiftyS26.Models
{
    public class MobileDashboardResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }

        public MobileDashboardStaffDto Staff { get; set; }
        public MobileDashboardStatsDto Statistics { get; set; }

        public List<MobileDashboardAttentionDto> AttentionOrders { get; set; }
        public List<MobileDashboardActivityDto> RecentActivity { get; set; }
    }

    public class MobileDashboardStaffDto
    {
        public int StoreStaffId { get; set; }
        public string FullName { get; set; }
        public string Role { get; set; }
    }

    public class MobileDashboardStatsDto
    {
        public int Preparing { get; set; }
        public int ReadyForPickup { get; set; }
        public int DeadlineApproaching { get; set; }
        public int UncollectedReview { get; set; }
    }

    public class MobileDashboardAttentionDto
    {
        public int OrderId { get; set; }
        public string ReferenceNumber { get; set; }
        public string MemberName { get; set; }
        public string Status { get; set; }
        public DateTime? PickupDeadline { get; set; }
        public string AttentionType { get; set; }
    }

    public class MobileDashboardActivityDto
    {
        public int EventId { get; set; }
        public int OrderId { get; set; }
        public string ReferenceNumber { get; set; }
        public string EventType { get; set; }
        public string Description { get; set; }
        public string PerformedBy { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}