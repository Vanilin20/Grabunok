# Тести HeistSim поза Unity

Ті самі файли, що й EditMode-тести Unity (`Assets/Tests/EditMode`), зібрані звичайним .NET SDK.
Потрібні, коли редактор Unity недоступний (CI, нічна перевірка): логіка справи — чистий C# без рушія.

```bash
dotnet test tools/heist_tests
```

`LangVersion 9.0` — як у Unity 6, `TreatWarningsAsErrors` — 0 попереджень.
