# MediaSorter

Десктопное приложение для автоматической сортировки фото и видео по дате съёмки.

Сканирует указанную папку, извлекает дату съёмки из метаданных (EXIF, QuickTime, XMP), имени файла или даты файловой системы и раскладывает файлы по папкам `dd.MM.yyyy`.

## Возможности

- **Извлечение даты** из EXIF (DateTimeOriginal, DateTimeDigitized, DateTime), QuickTime (MOV, MP4, HEIC), XMP, имени файла, даты создания/изменения файла
- **Поддержка форматов**: JPG, PNG, HEIC, HEIF, TIFF, BMP, WebP + видео: MP4, MOV, AVI, MKV, WMV, WebM, MTS, M2TS, MPEG, 3GP
- **Параллельная обработка** файлов (настраиваемое количество потоков)
- **Распознавание панорам**: группировка последовательных кадров панорам для совместного перемещения
- **Разрешение коллизий**: переименование с хешем, перезапись, пропуск дубликатов
- **Предпросмотр** плана перемещения перед выполнением
- **Журнал отката**: JSONL-файл с записями всех перемещений для возможности отката
- **Очистка пустых папок**: после перемещения или по кнопке
- **Single-file EXE**: самодостаточный .exe без установки (не требует .NET Runtime)

## Системные требования

- Windows 10 / 11 (64-bit)
- ~80 MB свободного места

## Сборка из исходников

```bash
# Требуется .NET 8 SDK

# Сборка
dotnet build src\MediaSorter\MediaSorter.csproj --configuration Release

# Тесты
dotnet test tests\MediaSorter.Tests\MediaSorter.Tests.csproj --configuration Release

# Публикация single-file EXE
dotnet publish src\MediaSorter\MediaSorter.csproj --configuration Release --output publish
```

Готовый EXE: `publish\MediaSorter.exe`

## Использование

1. Запустите `MediaSorter.exe`
2. Выберите папку с фото/видео через кнопку "Обзор..."
3. Нажмите "Сканировать" — программа найдёт все медиафайлы и извлечёт даты
4. Просмотрите результат в списке или через "Предпросмотр"
5. Нажмите "Начать перемещение" — файлы будут разложены по папкам `dd.MM.yyyy`

Файлы без даты попадают в папку `дата съемки неопределена`.

## Технологии

- .NET 8 / WPF (Windows Presentation Foundation)
- MetadataExtractor (чтение EXIF/QuickTime/XMP метаданных)
- CommunityToolkit.Mvvm (MVVM)
- Microsoft.Extensions.Hosting (DI, логирование)
- xUnit (тестирование)

## Лицензия

MIT
