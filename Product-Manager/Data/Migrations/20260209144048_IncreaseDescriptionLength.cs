using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Product_Manager.Migrations
{
    /// <inheritdoc />
    public partial class IncreaseDescriptionLength : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Intentional no-op migration: originally named IncreaseDescriptionLength,
            // but no schema changes are required. This statement is used only to make
            // the migration explicit and non-empty.
            migrationBuilder.Sql("/* No-op migration: IncreaseDescriptionLength has no schema changes. */");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Matching no-op for the Down migration to document that nothing is reverted.
            migrationBuilder.Sql("/* No-op rollback: IncreaseDescriptionLength made no schema changes. */");
        }
    }
}
