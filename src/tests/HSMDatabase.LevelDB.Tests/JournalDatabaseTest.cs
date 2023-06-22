using System.Text;
using System.Text.Json;
using HSMDatabase.AccessManager.DatabaseEntities;
using HSMServer.Core.DataLayer;
using HSMServer.Core.Tests.DatabaseTests;
using HSMServer.Core.Tests.Infrastructure;
using HSMServer.Core.Tests.MonitoringCoreTests.Fixture;
using Xunit;

namespace HSMDatabase.LevelDB.Tests
{
    public class JournalDatabaseTest : DatabaseCoreTestsBase<JournalDatabaseFixture>, IClassFixture<DatabaseRegisterFixture>
    {
        private readonly IDatabaseCore _databaseCore;

        public JournalDatabaseTest(JournalDatabaseFixture fixture, DatabaseRegisterFixture registerFixture) : base(fixture, registerFixture)
        {
            _databaseCore = _databaseCoreManager.DatabaseCore;
        }

        [Fact]
        public async void Test2()
        {
            var guid = Guid.NewGuid();
            var key = new Key(guid, 599506272170000000);
            var journal = new JournalEntity()
            {
                Id = key,
                Value = "Test1"
            };
            
            var journal2 = new JournalEntity()
            {
                Id = new Key(guid, 599509728340000000),
                Value = "Test2"
            };
            
            var journal3 = new JournalEntity()
            {
                Id = new Key(guid, 599527872510000000),
                Value = "Test3"
            };

            var start = new DateTime(599506272170000000).AddMilliseconds(-100);
            var end = new DateTime(599527872510000000);
            
            _databaseCore.AddJournalValue(journal2);
            _databaseCore.AddJournalValue(journal3);
            _databaseCore.AddJournalValue(journal);
            
            var pages = await _databaseCore.GetJournalValuesPage(guid, DateTime.MinValue, DateTime.MaxValue, 50000).Flatten();
            
            foreach (var item in pages)
            {
                var b = JsonSerializer.Deserialize<JournalEntity>(item);
                var asd = 1;
            }
        }
        
        [Theory]
        [InlineData(5)]
        [InlineData(11)]
        public async Task GetValues_Count_Test(int sensorsCount = 5)
        {
            const int historyValuesCount = 101;
            var sensorId = Guid.NewGuid();
            var journals = GenerateJournalEntities(sensorId, sensorsCount);

            foreach (var journal in journals)
            {
                _databaseCore.AddJournalValue(journal);
            }

            var actualJournals = (await _databaseCore.GetJournalValuesPage(sensorId, DateTime.MinValue, DateTime.MaxValue, historyValuesCount)
                .Flatten()).Select(x => JsonSerializer.Deserialize<JournalEntity>(x)).OrderBy(x => x.Id.Time).ToList();
            
            Assert.Equal(journals.Count, actualJournals.Count);

            for (int i = 0; i < sensorsCount; i++)
            {
                var actual = actualJournals[i];
                var expected = journals[i];
                Assert.Equal(expected.Value, actual.Value);
                Assert.Equal(expected.Id, actual.Id);
            }
        }

        private List<JournalEntity> GenerateJournalEntities(Guid sensorId, int count)
        {
            List<JournalEntity> result = new(count);

            for (int i = 0; i < count; i++)
            {
                result.Add(new JournalEntity()
                {
                    Id = new Key(sensorId, DateTime.UtcNow.Ticks),
                    Value = $"TEST_{i}"
                });
            }

            return result;
        }
    }
}