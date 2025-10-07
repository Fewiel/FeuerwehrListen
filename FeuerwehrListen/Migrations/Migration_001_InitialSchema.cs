using FluentMigrator;

namespace FeuerwehrListen.Migrations;

[Migration(1)]
public class Migration_001_InitialSchema : Migration
{
    public override void Up()
    {
        Create.Table("AttendanceList")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("Title").AsString(200).NotNullable()
            .WithColumn("Unit").AsString(100).NotNullable()
            .WithColumn("Description").AsString(500).NotNullable()
            .WithColumn("CreatedAt").AsDateTime().NotNullable()
            .WithColumn("Status").AsInt32().NotNullable()
            .WithColumn("ClosedAt").AsDateTime().Nullable()
            .WithColumn("IsArchived").AsBoolean().NotNullable().WithDefaultValue(false);

        Create.Table("OperationList")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("OperationNumber").AsString(100).NotNullable()
            .WithColumn("Keyword").AsString(200).NotNullable()
            .WithColumn("AlertTime").AsDateTime().NotNullable()
            .WithColumn("CreatedAt").AsDateTime().NotNullable()
            .WithColumn("Status").AsInt32().NotNullable()
            .WithColumn("ClosedAt").AsDateTime().Nullable()
            .WithColumn("IsArchived").AsBoolean().NotNullable().WithDefaultValue(false);

        Create.Table("AttendanceEntry")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("AttendanceListId").AsInt32().NotNullable()
            .WithColumn("NameOrId").AsString(100).NotNullable()
            .WithColumn("EnteredAt").AsDateTime().NotNullable();

        Create.Table("OperationEntry")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("OperationListId").AsInt32().NotNullable()
            .WithColumn("NameOrId").AsString(100).NotNullable()
            .WithColumn("Vehicle").AsString(100).NotNullable()
            .WithColumn("Function").AsInt32().NotNullable()
            .WithColumn("WithBreathingApparatus").AsBoolean().NotNullable()
            .WithColumn("EnteredAt").AsDateTime().NotNullable();

        Create.Table("User")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("Username").AsString(50).NotNullable()
            .WithColumn("PasswordHash").AsString(200).NotNullable()
            .WithColumn("FirstName").AsString(100).NotNullable()
            .WithColumn("LastName").AsString(100).NotNullable()
            .WithColumn("Email").AsString(200).NotNullable()
            .WithColumn("Role").AsInt32().NotNullable()
            .WithColumn("CreatedAt").AsDateTime().NotNullable();

        Insert.IntoTable("User").Row(new
        {
            Username = "admin",
            PasswordHash = "jGl25bVBBBW96Qi9Te4V37Fnqchz/Eu4qB9vKrRIqRg=",
            FirstName = "Admin",
            LastName = "User",
            Email = "admin@feuerwehr.local",
            Role = 2,
            CreatedAt = DateTime.Now
        });

        Create.Table("ApiKey")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("Key").AsString(100).NotNullable()
            .WithColumn("Description").AsString(500).NotNullable()
            .WithColumn("CreatedAt").AsDateTime().NotNullable()
            .WithColumn("IsActive").AsBoolean().NotNullable().WithDefaultValue(true);

        Create.Table("ScheduledList")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("Type").AsInt32().NotNullable()
            .WithColumn("Title").AsString(200).NotNullable()
            .WithColumn("Unit").AsString(100).NotNullable()
            .WithColumn("Description").AsString(500).NotNullable()
            .WithColumn("OperationNumber").AsString(100).NotNullable()
            .WithColumn("Keyword").AsString(200).NotNullable()
            .WithColumn("ScheduledEventTime").AsDateTime().NotNullable()
            .WithColumn("MinutesBeforeEvent").AsInt32().NotNullable()
            .WithColumn("IsProcessed").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("CreatedAt").AsDateTime().NotNullable();
    }

    public override void Down()
    {
        Delete.Table("ScheduledList");
        Delete.Table("ApiKey");
        Delete.Table("User");
        Delete.Table("OperationEntry");
        Delete.Table("AttendanceEntry");
        Delete.Table("OperationList");
        Delete.Table("AttendanceList");
    }
}

