# HolidayApp — planeador anual de férias

Calendário com os 12 meses numa só página para marcar férias, ver os feriados
nacionais portugueses e controlar o saldo de dias úteis.

## Requisitos

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Visual Studio 2022 (17.14+) / Visual Studio 2026, Rider ou VS Code com C# Dev Kit

## Arrancar

```bash
dotnet run --project src/HolidayApp.Web
```

Abre http://localhost:5180. A base de dados SQLite (`holidayapp.db`) é criada
automaticamente na primeira execução.

No Visual Studio: abre `HolidayApp.slnx`, define **HolidayApp.Web** como projeto
de arranque e carrega em F5.

Testes (regras de calendário e validação, mais testes de integração à API com
uma base de dados SQLite temporária):

```bash
dotnet test
```

## Publicar (Azure App Service)

Fora de desenvolvimento a API **só aceita utilizadores autorizados**. O login é
feito pela *Authentication* do App Service (Easy Auth), que fica à frente da app:

1. Publica a app (Visual Studio → botão direito em **HolidayApp.Web** → **Publish**
   → **Azure App Service (Linux)**).
2. No portal do Azure, em **Authentication**, adiciona um fornecedor (Microsoft ou
   Google) e escolhe *Require authentication*.
3. Em **Configuration → Application settings** acrescenta os emails autorizados:
   `Auth__AllowedEmails__0` = `o.teu@email.com` (e `__1`, `__2`… para mais pessoas).
4. Aponta a base de dados para a pasta persistente:
   `ConnectionStrings__Default` = `Data Source=/home/data/holidayapp.db`.

Sem a *Authentication* ligada a API recusa todos os pedidos (401), e com outra
conta que não esteja na lista responde 403. Cada pessoa autorizada vê só os seus
dados. O Leaflet e a fonte Inter são servidos pela própria app (`wwwroot/lib`),
sem pedidos a CDNs nem ao Google Fonts.

## O que já faz

- Vista anual com os 12 meses (semana a começar à segunda)
- Clicar num dia ou **arrastar** sobre vários para criar uma marcação
- Clicar numa marcação para editar ou apagar
- Tipos: Férias, Pessoal, Outro (só "Férias" desconta ao saldo)
- Feriados nacionais calculados automaticamente para qualquer ano
  (incluindo Sexta-feira Santa, Páscoa e Corpo de Deus)
- Carnaval e feriado municipal opcionais (página **Definições**)
- Contagem de dias úteis: fins de semana e feriados não contam
- Saldo anual (22 dias por omissão) e sugestões de **pontes** com marcação num clique
- **Organizar férias**: em cada marcação de férias, o botão "Organizar férias"
  abre uma página com destino, reservas (códigos, ligações e valor), plano dia
  a dia, lista de tarefas e orçamento (previsto vs. gasto, incluindo o valor
  das reservas)
- **Mapa dos locais a visitar**: marcar locais clicando no mapa ou procurando
  pelo nome, arrastar marcadores, escolher o dia da visita (aparece no plano dia
  a dia) e marcar como visitado. Usa [Leaflet](https://leafletjs.com) e os mapas
  do [OpenStreetMap](https://www.openstreetmap.org), sem chave de API; a pesquisa
  e o centro inicial (destino) usam o Nominatim do OpenStreetMap, por isso esses
  textos são enviados a esse serviço
- Modo claro/escuro automático e layout adaptado a telemóvel

## Arquitetura

```
src/
  HolidayApp.Shared/          DTOs, enums, validação e cálculo de dias úteis
                             (partilhado entre servidor e navegador)
  HolidayApp.Domain/          Entidades e regras de negócio: feriados PT,
                             Páscoa, sugestão de pontes. Sem dependências externas.
  HolidayApp.Infrastructure/  EF Core (SQLite), serviços da aplicação
  HolidayApp.Web/             Servidor ASP.NET Core: API REST em /api + aloja o Blazor
  HolidayApp.Client/          Interface Blazor WebAssembly (corre no navegador)
tests/
  HolidayApp.Domain.Tests/    Testes xUnit das regras de calendário e da validação
  HolidayApp.Api.Tests/       Testes de integração à API (WebApplicationFactory + SQLite)
```

Os estilos globais (cores, botões, campos, cartões) estão em
`HolidayApp.Web/wwwroot/css/app.css`; os de cada página e componente estão ao
lado do ficheiro, em `.razor.css` (CSS isolado do Blazor).

A interface fala com o servidor **só pela API**, por isso no futuro podes
acrescentar uma app móvel (MAUI, React Native…) que usa os mesmos endpoints.

### API

| Método | Rota | Descrição |
|---|---|---|
| GET | `/api/events?year=2026` | Marcações do ano |
| GET | `/api/events/{id}` | Uma marcação |
| POST | `/api/events` | Criar marcação |
| PUT | `/api/events/{id}` | Atualizar marcação |
| DELETE | `/api/events/{id}` | Apagar marcação (e o seu planeamento) |
| GET / PUT | `/api/events/{id}/plan` | Planeamento das férias |
| GET | `/api/vacations` | Todas as férias com o resumo do planeamento |
| GET | `/api/calendar/{ano}/holidays` | Feriados do ano |
| GET | `/api/calendar/{ano}/summary` | Saldo de férias e pontes |
| GET / PUT | `/api/settings` | Definições do utilizador |

Em desenvolvimento, a especificação OpenAPI está em `/openapi/v1.json`.

## Preparado para escalar

O projeto já tem as peças no sítio para crescer sem reescrever:

**1. Vários utilizadores.** Todas as tabelas têm `UserId` e o `AppDbContext`
aplica um filtro global para cada utilizador ver só os seus dados. Em
desenvolvimento o utilizador é sempre `local`; publicada, vem do login do App
Service (`Identity/AppServiceAuthentication.cs`), e a lista `Auth:AllowedEmails`
diz quem pode entrar. Para outro tipo de login (ASP.NET Core Identity, Entra ID
direto…) basta trocar o esquema de autenticação: o `HttpCurrentUser` lê o
`NameIdentifier` das claims e não precisa de mudar.

**2. Outra base de dados.** O fornecedor vem do `appsettings.json`
(`Database:Provider`). Para PostgreSQL: instala
`Npgsql.EntityFrameworkCore.PostgreSQL` no projeto Infrastructure, descomenta a
linha `UseNpgsql` em `DependencyInjection.cs` e muda a connection string.
As migrações atuais foram geradas para SQLite: ao mudar de fornecedor, gera-as
de novo.

**3. Migrações.** A base de dados é criada e atualizada com migrações do EF Core
(`Infrastructure/Persistence/Migrations`), aplicadas automaticamente ao arrancar.
Bases de dados antigas, criadas com `EnsureCreated`, são reconhecidas e mantêm
os dados. Quando mudares o modelo (entidades ou configurações), cria uma nova
migração:

```bash
dotnet tool restore
dotnet dotnet-ef migrations add NomeDaAlteracao --project src/HolidayApp.Infrastructure --startup-project src/HolidayApp.Web --output-dir Persistence/Migrations
```

**4. Partilhar com equipa/família.** Próximo passo natural: entidade `Team`
com membros e uma vista que mostra as férias de todos sobrepostas no mesmo
calendário.

## Ideias para a próxima iteração

- Login e partilha de calendário
- Exportar para `.ics` (Google Calendar / Outlook)
- Saldo transitado do ano anterior
- Feriados de outros países (nova implementação de `IHolidayProvider`)
