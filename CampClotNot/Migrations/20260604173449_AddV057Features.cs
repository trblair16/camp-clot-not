using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CampClotNot.Migrations
{
    /// <inheritdoc />
    public partial class AddV057Features : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "MustChangePassword",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<byte[]>(
                name: "PdfData",
                table: "InfoPages",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PdfContentType",
                table: "InfoPages",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PdfVisibleRoles",
                table: "InfoPages",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "MustChangePassword", table: "Users");
            migrationBuilder.DropColumn(name: "PdfData",            table: "InfoPages");
            migrationBuilder.DropColumn(name: "PdfContentType",     table: "InfoPages");
            migrationBuilder.DropColumn(name: "PdfVisibleRoles",    table: "InfoPages");
        }
    }
}
