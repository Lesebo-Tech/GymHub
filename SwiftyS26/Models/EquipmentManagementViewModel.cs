using System.Collections.Generic;

namespace SwiftyS26.Models
{
    public class EquipmentManagementViewModel
    {
        public List<Equipment> AllEquipment { get; set; }
        public List<Equipment> DueForInspection { get; set; }
        public List<Inspection> InspectionHistory { get; set; }
        public List<Repair> OpenRepairs { get; set; }
        public List<Technician> Technicians { get; set; }
        public List<Vendor> Vendors { get; set; }
        public Dictionary<int, Technician> RecommendedTechnicians { get; set; }

        // ── Post-repair verification: repairs completed by a technician
        // (RepairStatus == "AwaitingInspection") that need a DIFFERENT
        // technician to verify the fix before the equipment goes back
        // into service. Rendered inside the Inspections Due tab, sorted
        // above routine due items. ──
        public List<Repair> AwaitingInspectionRepairs { get; set; }

        // ── Preventive Maintenance — equipment due/overdue for scheduled
        // maintenance, and the full history of maintenance visits. ──
        public List<Equipment> DueForMaintenance { get; set; }
        public List<Maintenance> MaintenanceHistory { get; set; }
    }
}