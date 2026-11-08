using FluentMigrator;

namespace FeuerwehrListen.Migrations;

/// <summary>Themenbereiche und Bereiche des Fuehrungskreises (ueber Sitzungen hinweg).</summary>
[Migration(41)]
public class Migration_041_LeadershipTopics : Migration
{
    public override void Up()
    {
        Create.Table("LeadershipTopic")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("Circle").AsInt32().NotNullable()
            .WithColumn("Kind").AsInt32().NotNullable()
            .WithColumn("Title").AsString(300).NotNullable()
            .WithColumn("IsArchived").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("CreatedAt").AsDateTime().NotNullable();
        Alter.Table("LeadershipItem")
            .AddColumn("TopicId").AsInt32().Nullable()
            .AddColumn("AreaId").AsInt32().Nullable();
    }

    public override void Down()
    {
        Delete.Column("TopicId").FromTable("LeadershipItem");
        Delete.Column("AreaId").FromTable("LeadershipItem");
        Delete.Table("LeadershipTopic");
    }
}
