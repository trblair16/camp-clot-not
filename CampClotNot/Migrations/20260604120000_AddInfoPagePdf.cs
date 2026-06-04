using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CampClotNot.Migrations
{
    /// <inheritdoc />
    public partial class AddInfoPagePdf : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
            migrationBuilder.DropColumn(name: "PdfData",         table: "InfoPages");
            migrationBuilder.DropColumn(name: "PdfContentType",  table: "InfoPages");
            migrationBuilder.DropColumn(name: "PdfVisibleRoles", table: "InfoPages");
        }
    }
}
