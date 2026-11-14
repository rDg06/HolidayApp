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

Testes (regras de calendário e validação, interface com bUnit e integração à API
com uma base de dados SQLite temporária):

```bash
dotnet test
```

## Servidor de casa (Windows)

Para usar só na rede de casa, num PC Windows sempre ligado (ex.: o mesmo do Home
Assistant). Precisa do Git e do .NET 10 SDK (`winget install --id Microsoft.DotNet.SDK.10 -e`).

```bash
cd $HOME\Documents
git clone https://github.com/rDg06/HolidayApp.git
cd HolidayApp
dotnet publish src/HolidayApp.Web -c Release -o C:\HolidayApp\app
mkdir C:\HolidayApp\dados
```

`C:\HolidayApp\iniciar.bat` (abre na porta 5180, sem login, dados fora da pasta da app):

```bat
@echo off
title HolidayApp
set ASPNETCORE_URLS=http://0.0.0.0:5180
set ASPNETCORE_ENVIRONMENT=Production
set Auth__Disabled=true
set ConnectionStrings__Default=Data Source=C:\HolidayApp\dados\holidayapp.db
cd /d C:\HolidayApp\app
dotnet HolidayApp.Web.dll
```

Uma vez, num PowerShell de administrador, abrir a porta na rede privada:

```bash
New-NetFirewallRule -DisplayName "HolidayApp" -Direction Inbound -Protocol TCP -LocalPort 5180 -Action Allow -Profile Private
```

Abre-se em `http://IP-DO-SERVIDOR:5180`. Para atualizar: fechar a app, `git pull`,
o mesmo `dotnet publish` e voltar a correr o `iniciar.bat` (os dados em
`C:\HolidayApp\dados` mantêm-se). Sem login, quem estiver na rede de casa pode
abrir a app; sem HTTPS, o modo sem internet e o "Instalar app" do telemóvel não
funcionam.

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
  abre uma página com separadores por baixo das datas: Locais (destino e mapa),
  Reservas (códigos, ligações e valor), Tarefas, Orçamento (previsto vs. gasto,
  incluindo o valor das reservas, e o gráfico "Onde gastei mais") e Dia a dia
- **Página Férias** com todas as viagens: dias, reservas, locais, tarefas e orçamento
- **Mapa dos locais** ([Leaflet](https://leafletjs.com) e mapas do
  [OpenStreetMap](https://www.openstreetmap.org), sem chave de API):
  - marcar locais clicando no mapa ou procurando pelo nome, arrastar marcadores
  - dia da visita (aparece no plano dia a dia), "visitado", notas e ligação
  - **tipos** com ícone próprio: visita, alojamento, aeroporto, metro, autocarro,
    comboio; aeroportos e estações sem dia aparecem em todos os dias
  - **alojamentos** com primeiro e último dia, para viagens por várias cidades
    ou países; as reservas de alojamento têm "Pôr no mapa"
  - **filtro por dia** no mapa e na lista; durante a viagem abre no dia de hoje
  - **percurso por ruas** do dia (alojamento → locais pela ordem da lista →
    novo alojamento) com distância e tempo de cada troço, a pé, de carro ou de
    bicicleta; arrasta os locais para mudar a ordem
  - serviços usados: a pesquisa e o centro do destino usam o Nominatim e os
    percursos o serviço de rotas ([FOSSGIS](https://routing.openstreetmap.de)),
    ambos do OpenStreetMap, por isso esses textos e as coordenadas dos locais do
    dia são enviados a esses serviços; sem ligação fica a linha reta
- **Partilhar em PDF**: no planeador, "Partilhar (PDF)" abre um resumo da viagem
  (mapa, dia a dia, outros locais, reservas) pronto a guardar como PDF pela
  impressão do navegador. Valores, códigos/ligações das reservas e notas só
  entram se escolhidos
- Modo claro/escuro automático e layout adaptado a telemóvel
- **Instalável e sem internet** (PWA): no telemóvel, "Adicionar ao ecrã
  principal". Sem ligação, abre a última versão vista neste aparelho (só
  leitura: gravar precisa de internet; os mapas do OpenStreetMap não ficam
  guardados)

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
  HolidayApp.Client.Tests/    Testes da interface (bUnit): mapa, filtro, alojamentos, reservas
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

- "O que há perto" do alojamento (restaurantes, supermercados, farmácias…)
- Informação automática dos locais (resumo e foto da Wikipédia, horários)
- Mapa de todas as viagens na página Férias
- Login e partilha de calendário
- Saldo transitado do ano anterior
- Feriados de outros países (nova implementação de `IHolidayProvider`)
