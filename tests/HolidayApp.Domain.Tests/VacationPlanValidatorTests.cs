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
}
