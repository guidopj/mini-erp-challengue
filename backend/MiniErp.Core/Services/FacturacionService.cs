using Microsoft.EntityFrameworkCore;
using MiniErp.Core.Data;
using MiniErp.Core.Models;

namespace MiniErp.Core.Services;

public class FacturacionService
{
    private readonly AppDbContext _db;
    private readonly PresupuestoService _presupuestos;
    private readonly NumeracionService _numeracion;

    public FacturacionService(AppDbContext db, PresupuestoService presupuestos, NumeracionService numeracion)
    {
        _db = db;
        _presupuestos = presupuestos;
        _numeracion = numeracion;
    }

    public async Task<Factura> FacturarAsync(int presupuestoId)
    {
        var presupuesto = await _db.Presupuestos
            .Include(p => p.Items)
            .FirstOrDefaultAsync(p => p.Id == presupuestoId)
            ?? throw new InvalidOperationException("El presupuesto no existe.");

        // 1. Validar estado
        if (presupuesto.Estado == EstadoPresupuesto.Facturado)
        {
            throw new InvalidOperationException("El presupuesto ya fue facturado.");
        }

        // 2. Validar vencimiento
        var vencimiento = presupuesto.Fecha.AddDays(presupuesto.ValidezDias);

        if (DateTime.UtcNow > vencimiento)
            throw new InvalidOperationException("El presupuesto está vencido y no se puede facturar.");

        // 3. Validar stock de todos los artículos
        foreach (var item in presupuesto.Items)
        {
            var articulo = await _db.Articulos
                .FirstAsync(a => a.Id == item.ArticuloId);

            if (articulo.StockActual < item.Cantidad)
            {
                throw new InvalidOperationException(
                    $"Stock insuficiente para el artículo {articulo.Id}."
                );
            }
        }

        // 4. Descontar stock
        foreach (var item in presupuesto.Items)
        {
            var articulo = await _db.Articulos
                .FirstAsync(a => a.Id == item.ArticuloId);

            articulo.StockActual -= item.Cantidad;
        }

        // 5. Calcular totales
        var totales = _presupuestos.CalcularTotales(presupuesto);

        // 6. Crear factura
        var factura = new Factura
        {
            Numero = await _numeracion.ProximoNumeroFacturaAsync(),
            Fecha = DateTime.UtcNow,
            PresupuestoId = presupuesto.Id,
            Subtotal = totales.Subtotal,
            Iva = totales.Iva,
            Total = totales.Total
        };

        // 7. Actualizar estado
        presupuesto.Estado = EstadoPresupuesto.Facturado;

        // 8. Persistir cambios
        _db.Facturas.Add(factura);

        await _db.SaveChangesAsync();

        return factura;
    }
}
