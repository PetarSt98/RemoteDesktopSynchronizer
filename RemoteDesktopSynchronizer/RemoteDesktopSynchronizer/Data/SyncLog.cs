using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;


namespace RemoteDesktopCleaner.Data
{
    [Table("SynchronizedRAP")]
    public partial class SynchronizedRAP
    {
        [Key]
        [Column(Order = 0)]
        public string idSyncProcess { get; set; }

        [Key]
        [Column(Order = 1)]
        [StringLength(100)]
        [ForeignKey("rap")]
        public string RAPName { get; set; }

        public bool success { get; set; }

        public virtual rap rap { get; set; }

        public override string ToString()
        {
            return JsonSerializer.Serialize(this);
        }
    }
}
