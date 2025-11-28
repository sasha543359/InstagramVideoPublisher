using System.Text;
using InstagramBulkPublisher.Models;
using Newtonsoft.Json;

namespace InstagramBulkPublisher.Services
{
    /// <summary>
    /// Сервис для работы с аккаунтами (CSV → JSON)
    /// </summary>
    public interface IAccountService
    {
        /// <summary>
        /// Загрузить аккаунты из CSV и сохранить в JSON
        /// </summary>
        Task<List<InstagramAccount>> LoadAccountsFromCsvAsync(string csvPath, string jsonPath);

        /// <summary>
        /// Загрузить аккаунты из JSON
        /// </summary>
        Task<List<InstagramAccount>> LoadAccountsFromJsonAsync(string jsonPath);

        /// <summary>
        /// Проверить есть ли готовый JSON файл
        /// </summary>
        bool JsonExists(string jsonPath);
    }

    public class AccountService : IAccountService
    {
        public async Task<List<InstagramAccount>> LoadAccountsFromCsvAsync(string csvPath, string jsonPath)
        {
            if (!File.Exists(csvPath))
            {
                throw new FileNotFoundException($"CSV файл не найден: {csvPath}");
            }

            var accounts = new List<InstagramAccount>();
            var lines = await File.ReadAllLinesAsync(csvPath, Encoding.UTF8);

            // Пропускаем заголовок если есть
            var startIndex = 0;
            if (lines.Length > 0)
            {
                var firstLine = lines[0].ToLower();
                if (firstLine.Contains("accountname") || firstLine.Contains("account_name") ||
                    firstLine.Contains("name") || firstLine.Contains("accountid"))
                {
                    startIndex = 1;
                }
            }

            for (int i = startIndex; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (string.IsNullOrWhiteSpace(line)) continue;

                var parts = line.Split(',');
                if (parts.Length >= 3)
                {
                    accounts.Add(new InstagramAccount
                    {
                        AccountName = parts[0].Trim().Trim('"'),
                        AccountId = parts[1].Trim().Trim('"'),
                        AccessToken = parts[2].Trim().Trim('"')
                    });
                }
            }

            // Сохраняем в JSON
            var json = JsonConvert.SerializeObject(accounts, Formatting.Indented);
            await File.WriteAllTextAsync(jsonPath, json, Encoding.UTF8);

            return accounts;
        }

        public async Task<List<InstagramAccount>> LoadAccountsFromJsonAsync(string jsonPath)
        {
            if (!File.Exists(jsonPath))
            {
                throw new FileNotFoundException($"JSON файл не найден: {jsonPath}");
            }

            var json = await File.ReadAllTextAsync(jsonPath, Encoding.UTF8);
            return JsonConvert.DeserializeObject<List<InstagramAccount>>(json)
                   ?? new List<InstagramAccount>();
        }

        public bool JsonExists(string jsonPath)
        {
            return File.Exists(jsonPath);
        }
    }
}