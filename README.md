# NapaMonitor — PostgreSQL Health Dashboard

A desktop application for monitoring the health and performance of a PostgreSQL database, with AI-powered insights using a locally running language model.

---

## Features

- **Real-time metrics collection** — active connections, cache hit ratio, database size, and slow queries, collected at a configurable interval
- **Historical trends** — all snapshots are persisted locally in JSON files and viewable in the History tab
- **Live charts** — line charts for connections, cache hit ratio, and database size over time
- **Monitoring alerts** — automatic warnings when metrics exceed defined thresholds (high connection usage, low cache hit ratio, slow queries detected)
- **AI analysis** — on-demand analysis of current metrics using a locally running LLaMA 3.2 model via Ollama, generating a health summary and optimization suggestions
- **Configurable collection interval** — set how frequently metrics are collected directly from the UI

---

## Architecture Overview

The application follows the **MVVM (Model-View-ViewModel)** pattern, standard for WPF applications:

```
NapaMonitor/
├── Models/          # Data classes: MetricSnapshot, SlowQuery, MonitorAlert
├── Services/        # Business logic
│   ├── DatabaseService.cs   # Connects to PostgreSQL and collects metrics
│   ├── StorageService.cs    # Persists snapshots to local JSON files
│   └── AiService.cs         # Sends metrics to Ollama and returns AI analysis
├── ViewModels/      # MainViewModel: bridges services and UI, handles state
├── Views/           # MainWindow XAML: the dashboard UI
└── Data/            # Runtime folder where JSON metric files are stored
```

**Key design decisions:**

- Metrics are collected asynchronously on a timer, keeping the UI responsive at all times
- Data is stored in one JSON file per day (e.g. `metrics_2026-05-21.json`), making it easy to inspect manually
- The AI model runs entirely locally via Ollama — no API keys, no internet required, no data leaves the machine
- Alert thresholds are evaluated after every collection cycle, giving immediate feedback on anomalies

---

## Metrics Collected

All metrics are read from PostgreSQL system views — no extensions or external tools required:

| Metric              | Source                 |
| ------------------- | ---------------------- |
| Active connections  | `pg_stat_activity`     |
| Max connections     | `SHOW max_connections` |
| Database size (MB)  | `pg_database_size()`   |
| Cache hit ratio (%) | `pg_stat_database`     |
| Slow queries (> 1s) | `pg_stat_activity`     |

---

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- [PostgreSQL](https://www.postgresql.org/download/) (local installation)
- [Ollama](https://ollama.com/download) with the `llama3.2` model

---

## Setup Instructions

### 1. Clone the repository

```bash
git clone https://github.com/YOUR_USERNAME/napa-monitor.git
cd napa-monitor
```

### 2. Set up PostgreSQL

Install PostgreSQL locally and create a database:

```sql
CREATE DATABASE napa_monitor;
```

### 3. Set up Ollama

Install Ollama from [https://ollama.com/download](https://ollama.com/download), then pull the model:

```bash
ollama pull llama3.2
```

Ollama starts automatically and runs a local server on `http://localhost:11434`.

### 4. Configure the application

Copy the example config file and fill in your PostgreSQL password:

```bash
cp appsettings.example.json appsettings.json
```

Edit `appsettings.json`:

```json
{
  "Database": {
    "ConnectionString": "Host=localhost;Port=5432;Database=napa_monitor;Username=postgres;Password=YOUR_PASSWORD"
  },
  "Ai": {
    "Provider": "Ollama"
  }
}
```

### 5. Run the application

Open the solution in JetBrains Rider (or Visual Studio 2022) and run the project, or use the CLI:

```bash
dotnet run
```

---

## How AI Is Used

The AI integration is intentionally **on-demand** rather than automatic — the user clicks "Analyze using AI" to trigger an analysis at any time.

When triggered, the application:

1. Takes the current snapshot and recent history from memory
2. Builds a structured plain-text prompt containing the metrics and trends
3. Sends the prompt to a locally running `llama3.2` model via the Ollama REST API
4. Displays the model's response directly in the UI

The prompt instructs the model to act as a PostgreSQL monitoring assistant and provide: a brief health summary, any detected anomalies, and specific optimization suggestions.

**Why Ollama?** Running the model locally means no API keys, no usage limits, no internet dependency, and no data leaving the machine. This makes the application fully self-contained and suitable for environments where sending database metrics to external services would be a concern.

---

## Tech Stack

- **Language:** C# / .NET 8
- **UI Framework:** WPF (Windows Presentation Foundation)
- **Database connector:** Npgsql
- **Charts:** LiveChartsCore.SkiaSharpView.WPF
- **AI:** Ollama (llama3.2, running locally)
- **Storage:** JSON files via System.Text.Json
