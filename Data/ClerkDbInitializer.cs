using Microsoft.EntityFrameworkCore;

namespace CheapClerk.Data;

public static class ClerkDbInitializer
{
    // EnsureCreated() no-ops on existing databases, so additive tables need
    // explicit DDL. Revisit with real EF migrations if the cache schema grows.
    // Raw DDL table names below must match their DbSet property names, since
    // that is what EF uses as the default table name.
    public static async Task EnsureSchemaAsync(ClerkDbContext db)
    {
        await db.Database.EnsureCreatedAsync();
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
