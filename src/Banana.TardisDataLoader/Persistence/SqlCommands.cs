namespace Banana.TardisDataLoader.Persistence;

public static class SqlCommands
{
    public static readonly string SetLocalSynchronousCommitOff = "SET LOCAL synchronous_commit = OFF;";
    public static string SetLocalWorkingMemory(int memoryMb) => $"SET LOCAL work_mem = '{memoryMb}MB';";

    public static class Instruments
    {
        public static readonly string Schema = File.ReadAllText("Persistence/Sql/Instruments/mkt_00_schema.sql");
        public static readonly string UpsertOne = File.ReadAllText("Persistence/Sql/Instruments/mkt_21_upsert_one.sql");

        public static readonly string GetCurrentInstrumentBySymbol =
            File.ReadAllText("Persistence/Sql/Instruments/mkt_40_get_current_instrument.sql");

        public static readonly string GetCurrentInstrumentById =
            File.ReadAllText("Persistence/Sql/Instruments/mkt_41_get_instrument_by_id.sql");

        public static readonly string ListInstrumentsByType =
            File.ReadAllText("Persistence/Sql/Instruments/mkt_42_list_current_by_exchange_and_type.sql");
    }

    public static class HistoricData
    {
        public static class Trades
        {
            public static readonly string InsertBatch =
                File.ReadAllText("Persistence/Sql/History/md_03_insert_trades.sql");

            public static readonly string StageCreate =
                File.ReadAllText("Persistence/Sql/History/md_trades_stage_create.sql");

            public static readonly string StageMerge =
                File.ReadAllText("Persistence/Sql/History/md_trades_stage_flush.sql");

            public static readonly string BeginBinaryImport =
                "COPY trades_stage (ts, instrument_id, side, price, quantity, trade_id) FROM STDIN (FORMAT BINARY)";
        }

        public static class LevelUpdates
        {
            public static readonly string InsertLevelsBatch =
                File.ReadAllText("Persistence/Sql/History/md_03_insert_levels.sql");

            public static readonly string StageCreate =
                File.ReadAllText("Persistence/Sql/History/md_levels_stage_create.sql");

            public static readonly string StageMerge =
                File.ReadAllText("Persistence/Sql/History/md_levels_stage_flush.sql");

            public static readonly string BeginBinaryImport =
                "COPY level_updates_stage (ts, instrument_id, side, price, quantity, is_snapshot, update_seq) FROM STDIN (FORMAT BINARY)";
        }

        public static class Utils
        {
            public static readonly string ListMissingDays =
                File.ReadAllText("Persistence/Sql/History/md_50_list_missing_days.sql");

            public static readonly string ExistsFeedOnDay =
                File.ReadAllText("Persistence/Sql/History/md_51_exists_feed_on_day.sql");

            public static readonly string GetTradesDayInfo =
                File.ReadAllText("Persistence/Sql/History/md_52_get_trades_day_info.sql");

            public static readonly string GetLevelsDayInfo =
                File.ReadAllText("Persistence/Sql/History/md_52_get_levels_day_info.sql");
        }
    }
}
