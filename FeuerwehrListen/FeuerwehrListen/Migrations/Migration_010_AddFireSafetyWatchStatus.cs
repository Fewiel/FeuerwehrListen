using FluentMigrator;

namespace FeuerwehrListen.Migrations
{
    [Migration(10)]
    public class Migration_010_AddFireSafetyWatchStatus : Migration
    {
        public override void Up()
        {
            Alter.Table("fire_safety_watches")
                .AddColumn("Status").AsInt32().NotNullable().WithDefaultValue(1)
                .AddColumn("ClosedAt").AsDateTime().Nullable()
                .AddColumn("IsArchived").AsBoolean().NotNullable().WithDefaultValue(false);
        }

        public override void Down()
        {
            Delete.Column("Status").FromTable("fire_safety_watches");
            Delete.Column("ClosedAt").FromTable("fire_safety_watches");
            Delete.Column("IsArchived").FromTable("fire_safety_watches");
        }
    }
}
