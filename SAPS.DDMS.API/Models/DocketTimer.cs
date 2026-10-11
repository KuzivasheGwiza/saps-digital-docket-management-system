using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SAPS.DDMS.API.Models
{
    public class DocketTimer
    {
        [Key]
        public int DocketTimerID { get; set; }

        public DateTime StartTime { get; set; } = DateTime.UtcNow;
        public DateTime? DurationLimit { get; set; }
        public DateTime? EndTime { get; set; }
        public bool IsCompleted { get; set; } = false;
        [ForeignKey("Docket")]
        public int FK_DocketID { get; set; }
        public virtual Docket Docket { get; set; }
    }
}
