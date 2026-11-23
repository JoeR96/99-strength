using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace A2S.Infrastructure.Migrations
{
    /// <summary>
    /// Data only (no schema change). Moves existing Stair finisher tracks (template key <c>stair-finisher</c>, the
    /// template's two stages) onto the new defaults: a 3 min easy warm-up (level 3, Z2) and cool-down (level 3, Z1–2)
    /// on both stages, continuous 5 → 15 min with the level step resetting to 10, intervals 6 → 10 rounds resetting
    /// to 8. The position is kept: volume is only clamped into the new range (stage 1 rounds below 6 become 6);
    /// level, qualifying count and step are untouched. The undo snapshot's stages get the same change so an undo
    /// does not bring the old ranges back. A migration rather than an on-read default: it runs once, so a warm-up
    /// the user later turns off (null) stays off. Down puts the old ranges back and drops the new keys.
    /// </summary>
    public partial class BackfillStairFinisherSegments : Migration
    {
        private const string Where = """
            "TemplateKey" = 'stair-finisher'
              AND jsonb_typeof("Stages") = 'array'
              AND jsonb_array_length("Stages") = 2
              AND "Stages"->0->>'Kind' = 'DoubleProgression'
              AND "Stages"->1->'DoubleProgression'->>'VolumeUnit' = 'Rounds'
            """;

        private const string WarmUp = """'{"Minutes":3,"Level":3,"Zone":"Z2"}'::jsonb""";
        private const string CoolDown = """'{"Minutes":3,"Level":3,"Zone":"Z1–2"}'::jsonb""";

        /// <summary>The new stages, built from <paramref name="stages"/> (a jsonb expression).</summary>
        private static string Upgrade(string stages) => $$"""
            jsonb_build_array(
              ({{stages}}->0) || jsonb_build_object(
                'WarmUp', COALESCE({{stages}}->0->'WarmUp', {{WarmUp}}),
                'CoolDown', COALESCE({{stages}}->0->'CoolDown', {{CoolDown}}),
                'DoubleProgression', ({{stages}}->0->'DoubleProgression') || '{"VolumeMin":5,"VolumeMax":15,"VolumeResetTo":10}'::jsonb),
              ({{stages}}->1) || jsonb_build_object(
                'WarmUp', COALESCE({{stages}}->1->'WarmUp', {{WarmUp}}),
                'CoolDown', COALESCE({{stages}}->1->'CoolDown', {{CoolDown}}),
                'DoubleProgression', ({{stages}}->1->'DoubleProgression') || '{"VolumeMin":6,"VolumeMax":10,"VolumeResetTo":8}'::jsonb))
            """;

        private static string Downgrade(string stages) => $$"""
            jsonb_build_array(
              (({{stages}}->0) - 'WarmUp' - 'CoolDown') || jsonb_build_object(
                'DoubleProgression', (({{stages}}->0->'DoubleProgression') - 'VolumeResetTo') || '{"VolumeMin":5,"VolumeMax":10}'::jsonb),
              (({{stages}}->1) - 'WarmUp' - 'CoolDown') || jsonb_build_object(
                'DoubleProgression', (({{stages}}->1->'DoubleProgression') - 'VolumeResetTo') || '{"VolumeMin":4,"VolumeMax":8}'::jsonb))
            """;

        /// <summary>The state with its volume clamped into the stage's range (stage 0 or 1).</summary>
        private static string Clamp(string state, int min0, int max0, int min1, int max1) => $$"""
            jsonb_set({{state}}, '{Volume}', to_jsonb(
              CASE WHEN ({{state}}->>'StageIndex')::int = 1
                THEN GREATEST({{min1}}, LEAST({{max1}}, ({{state}}->>'Volume')::numeric))
                ELSE GREATEST({{min0}}, LEAST({{max0}}, ({{state}}->>'Volume')::numeric))
              END))
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($$"""
                UPDATE "CardioTracks" SET
                  "Stages" = {{Upgrade("\"Stages\"")}},
                  "State" = {{Clamp("\"State\"", 5, 15, 6, 10)}},
                  "LastSnapshot" = CASE
                    WHEN "LastSnapshot" IS NULL OR jsonb_typeof("LastSnapshot"->'Stages') IS DISTINCT FROM 'array'
                      OR jsonb_array_length("LastSnapshot"->'Stages') <> 2 THEN "LastSnapshot"
                    ELSE jsonb_set("LastSnapshot", '{Stages}', {{Upgrade("(\"LastSnapshot\"->'Stages')")}})
                  END
                WHERE {{Where}}
                  AND NOT (("Stages"->0->'DoubleProgression') ? 'VolumeResetTo');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($$"""
                UPDATE "CardioTracks" SET
                  "Stages" = {{Downgrade("\"Stages\"")}},
                  "State" = {{Clamp("\"State\"", 5, 10, 4, 8)}},
                  "LastSnapshot" = CASE
                    WHEN "LastSnapshot" IS NULL OR jsonb_typeof("LastSnapshot"->'Stages') IS DISTINCT FROM 'array'
                      OR jsonb_array_length("LastSnapshot"->'Stages') <> 2 THEN "LastSnapshot"
                    ELSE jsonb_set("LastSnapshot", '{Stages}', {{Downgrade("(\"LastSnapshot\"->'Stages')")}})
                  END
                WHERE {{Where}}
                  AND (("Stages"->0->'DoubleProgression') ? 'VolumeResetTo');
                """);
        }
    }
}
