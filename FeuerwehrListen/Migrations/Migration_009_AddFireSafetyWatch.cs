using FluentMigrator;

namespace FeuerwehrListen.Migrations
{
    [Migration(9)]
    public class Migration_009_AddFireSafetyWatch : Migration
    {
        public override void Up()
        {
            Create.Table("fire_safety_watches")
                .WithColumn("Id").AsInt32().PrimaryKey().Identity()
                .WithColumn("Name").AsString().NotNullable()
                .WithColumn("Location").AsString().NotNullable()
                .WithColumn("EventDateTime").AsDateTime().NotNullable();

            Create.Table("fire_safety_watch_requirements")
                .WithColumn("Id").AsInt32().PrimaryKey().Identity()
                .WithColumn("FireSafetyWatchId").AsInt32().NotNullable().ForeignKey("fire_safety_watches", "Id")
                .WithColumn("FunctionDefId").AsInt32().NotNullable().ForeignKey("operation_function_defs", "Id")
                .WithColumn("Amount").AsInt32().NotNullable()
                .WithColumn("VehicleId").AsInt32().Nullable().ForeignKey("vehicles", "Id");

            Create.Table("fire_safety_watch_entries")
                .WithColumn("Id").AsInt32().PrimaryKey().Identity()
                .WithColumn("FireSafetyWatchId").AsInt32().NotNullable().ForeignKey("fire_safety_watches", "Id")
                .WithColumn("RequirementId").AsInt32().NotNullable().ForeignKey("fire_safety_watch_requirements", "Id")
                .WithColumn("MemberId").AsInt32().NotNullable().ForeignKey("members", "Id");
        }

        public override void Down()
        {
            Delete.Table("fire_safety_watch_entries");
            Delete.Table("fire_safety_watch_requirements");
            Delete.Table("fire_safety_watches");
        }
    }
}
