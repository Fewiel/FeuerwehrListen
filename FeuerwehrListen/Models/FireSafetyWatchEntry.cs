using LinqToDB.Mapping;

namespace FeuerwehrListen.Models
{
    [Table("fire_safety_watch_entries")]
    public class FireSafetyWatchEntry
    {
        [PrimaryKey, Identity]
        public int Id { get; set; }

        [Column("FireSafetyWatchId"), NotNull]
        public int FireSafetyWatchId { get; set; }

        [Column("RequirementId"), NotNull]
        public int RequirementId { get; set; }

        [Column("MemberId"), NotNull]
        public int MemberId { get; set; }

        [Association(ThisKey = nameof(MemberId), OtherKey = nameof(Models.Member.Id))]
        public Member Member { get; set; } = null!;
    }
}
