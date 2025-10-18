using LinqToDB.Mapping;

namespace FeuerwehrListen.Models
{
    [Table("fire_safety_watch_requirements")]
    public class FireSafetyWatchRequirement
    {
        [PrimaryKey, Identity]
        public int Id { get; set; }

        [Column("FireSafetyWatchId"), NotNull]
        public int FireSafetyWatchId { get; set; }

        [Column("FunctionDefId"), NotNull]
        public int FunctionDefId { get; set; }
        
        [Association(ThisKey = nameof(FunctionDefId), OtherKey = nameof(Models.OperationFunctionDef.Id))]
        public OperationFunctionDef FunctionDef { get; set; } = null!;

        [Column("Amount"), NotNull]
        public int Amount { get; set; }

        [Column("VehicleId")]
        public int? VehicleId { get; set; }
        
        [Association(ThisKey = nameof(VehicleId), OtherKey = nameof(Models.Vehicle.Id))]
        public Vehicle Vehicle { get; set; } = null!;
    }
}
