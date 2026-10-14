using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CampClotNot.Migrations
{
    /// <inheritdoc />
    public partial class AddThemeUploads : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BannerContentType",
                table: "Themes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "BannerData",
                table: "Themes",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LogoContentType",
                table: "Themes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "LogoData",
                table: "Themes",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Themes",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BannerContentType",
                table: "Themes");

            migrationBuilder.DropColumn(
                name: "BannerData",
                table: "Themes");

            migrationBuilder.DropColumn(
                name: "LogoContentType",
                table: "Themes");

            migrationBuilder.DropColumn(
                name: "LogoData",
                table: "Themes");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Themes");
        }
    }
}
