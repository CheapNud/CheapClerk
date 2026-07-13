using Microsoft.EntityFrameworkCore;

namespace CheapClerk.Data;

public static class ClerkDbInitializer
{
    // EnsureCreated() no-ops on existing databases, so additive tables need
    // explicit DDL. Revisit with real EF migrations if the cache schema grows.
    // Raw DDL table names below must match their DbSet property names, since
    // that is what EF uses as the default table name.
    // The additive DDL only applies to pre-suggestions SQLite databases created
    // before these tables existed. A Postgres database is always created fresh,
    // so EnsureCreatedAsync already covers the full current model there; a
    // future additive schema change will need a Postgres-dialect branch here
    // (or a move to real migrations).
    public static async Task EnsureSchemaAsync(ClerkDbContext db)
    {
        await db.Database.EnsureCreatedAsync();

        if (db.Database.IsSqlite())
        {
            await db.Database.ExecuteSqlRawAsync(
                """
                CREATE TABLE IF NOT EXISTS "CachedSuggestions" (
                    "DocumentId" INTEGER NOT NULL CONSTRAINT "PK_CachedSuggestions" PRIMARY KEY,
                    "ClassificationJson" TEXT NOT NULL,
                    "Confidence" REAL NOT NULL,
                    "SuggestedAtUtc" TEXT NOT NULL
                );
                """);
            await db.Database.ExecuteSqlRawAsync(
                """
                CREATE TABLE IF NOT EXISTS "NameTranslations" (
                    "Kind" TEXT NOT NULL,
                    "CanonicalName" TEXT NOT NULL,
                    "Culture" TEXT NOT NULL,
                    "Label" TEXT NOT NULL,
                    "TranslatedAtUtc" TEXT NOT NULL,
                    CONSTRAINT "PK_NameTranslations" PRIMARY KEY ("Kind", "CanonicalName", "Culture")
                );
                """);
        }
    }
}
