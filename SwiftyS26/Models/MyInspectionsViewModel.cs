using System.Collections.Generic;

namespace SwiftyS26.Models
{
    public class MyInspectionsViewModel
    {
        // Routine equipment inspections assigned to this technician
        public List<Equipment> RoutineAssigned { get; set; }

        // Post-repair / vendor-repair verifications assigned to this technician
        public List<Repair> VerificationAssigned { get; set; }
    }
}