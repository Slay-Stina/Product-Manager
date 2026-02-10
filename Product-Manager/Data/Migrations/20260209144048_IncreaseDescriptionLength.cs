using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Product_Manager.Migrations
{
    /// <inheritdoc />
    public partial class IncreaseDescriptionLength : Migration
    {
        /// <summary>
        /// Intentional no-op migration. Originally created to increase description length,
        /// but the actual schema change was applied in a subsequent migration.
        /// This migration is kept for history and migration chain integrity.
        /// </summary>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // No schema changes required
        }

        /// <summary>
        /// No-op rollback. No schema changes to revert.
        /// </summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No schema changes to revert
        }
    }
}
