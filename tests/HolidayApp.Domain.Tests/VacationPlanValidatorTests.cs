using HolidayApp.Shared;
using HolidayApp.Shared.Validation;

namespace HolidayApp.Domain.Tests;

public class VacationPlanValidatorTests
{
    [Fact]
    public void Aceita_um_planeamento_vazio()
    {
        Assert.Empty(VacationPlanValidator.Validate(new VacationPlanDto()));
    }

    [Theory]
    [InlineData("https://www.booking.com/reserva/123", true)]
    [InlineData("http://exemplo.pt", true)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("data:text/html,<script>", false)]
    [InlineData("www.exemplo.pt", false)]
    public void So_aceita_ligacoes_http(string url, bool valid)
    {
        Assert.Equal(valid, VacationPlanValidator.IsSafeUrl(url));
    }

    [Fact]
    public void Rejeita_reservas_sem_nome_e_tarefas_vazias()
    {
        var plan = new VacationPlanDto
        {
            Bookings = [new PlanBookingDto { Name = " " }],
            Tasks = [new PlanTaskDto { Text = "" }],
        };

        var errors = VacationPlanValidator.Validate(plan);

        Assert.Contains(nameof(VacationPlanDto.Bookings), errors.Keys);
        Assert.Contains(nameof(VacationPlanDto.Tasks), errors.Keys);
    }

    [Fact]
    public void Rejeita_valores_negativos_no_orcamento()
    {
        var plan = new VacationPlanDto { Expenses = [new PlanExpenseDto { Planned = -5 }] };

        Assert.Contains(nameof(VacationPlanDto.Expenses), VacationPlanValidator.Validate(plan).Keys);
    }

    [Fact]
    public void Rejeita_valor_negativo_numa_reserva()
    {
        var plan = new VacationPlanDto { Bookings = [new PlanBookingDto { Name = "Hotel", Cost = -1 }] };

        Assert.Contains(nameof(VacationPlanDto.Bookings), VacationPlanValidator.Validate(plan).Keys);
    }

    [Theory]
    [InlineData("2026-08-03", true)]
    [InlineData("2026-08-14", true)]
    [InlineData("2026-08-02", false)]
    [InlineData("2026-08-15", false)]
    public void So_aceita_reservas_nos_dias_das_ferias(string date, bool valid)
    {
        var plan = new VacationPlanDto { Bookings = [new PlanBookingDto { Name = "Hotel", Date = DateOnly.Parse(date) }] };

        var errors = VacationPlanValidator.Validate(plan, new DateOnly(2026, 8, 3), new DateOnly(2026, 8, 14));

        Assert.Equal(valid, errors.Count == 0);
    }

    [Theory]
    [InlineData(41.8902, 12.4922, true)]
    [InlineData(91, 0, false)]
    [InlineData(0, -181, false)]
    [InlineData(double.NaN, 0, false)]
    public void Valida_as_coordenadas_dos_locais(double lat, double lng, bool valid)
    {
        var plan = new VacationPlanDto { Places = [new PlanPlaceDto { Name = "Coliseu", Latitude = lat, Longitude = lng }] };

        Assert.Equal(valid, VacationPlanValidator.Validate(plan).Count == 0);
    }

    [Fact]
    public void Locais_precisam_de_nome_e_de_um_dia_das_ferias()
    {
        var plan = new VacationPlanDto
        {
            Places =
            [
                new PlanPlaceDto { Name = " ", Latitude = 41.9, Longitude = 12.5 },
                new PlanPlaceDto { Name = "Vaticano", Latitude = 41.9, Longitude = 12.45, Date = new DateOnly(2026, 9, 1) },
            ],
        };

        var errors = VacationPlanValidator.Validate(plan, new DateOnly(2026, 8, 3), new DateOnly(2026, 8, 14));

        Assert.Equal(2, errors[nameof(VacationPlanDto.Places)].Length);
    }

    [Fact]
    public void Aceita_reservas_sem_dia()
    {
        var plan = new VacationPlanDto { Bookings = [new PlanBookingDto { Name = "Seguro" }] };

        Assert.Empty(VacationPlanValidator.Validate(plan, new DateOnly(2026, 8, 3), new DateOnly(2026, 8, 14)));
    }

    [Theory]
    [InlineData(BookingType.Voo, ExpenseCategory.Transporte)]
    [InlineData(BookingType.Alojamento, ExpenseCategory.Alojamento)]
    [InlineData(BookingType.Atividade, ExpenseCategory.Atividades)]
    [InlineData(BookingType.Outro, ExpenseCategory.Outro)]
    public void Reserva_entra_na_categoria_certa_do_orcamento(BookingType type, ExpenseCategory expected)
    {
        Assert.Equal(expected, PlanBookingDto.BudgetCategory(type));
    }
}
