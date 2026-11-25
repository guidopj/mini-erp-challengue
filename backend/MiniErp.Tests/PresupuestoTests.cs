using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MiniErp.Core.Data;
using MiniErp.Core.Models;
using MiniErp.Core.Services;
using Xunit;

namespace MiniErp.Tests
{
    public class PresupuestoTests
    {
        private static (FacturacionService fact, AppDbContext db) Armar()
        {
            var db = TestDbFactory.Create();
            var numeracion = new NumeracionService(db);
            var presupuestos = new PresupuestoService(db, numeracion);
            var fact = new FacturacionService(db, presupuestos, numeracion);
            return (fact, db);
        }

        private static async Task<(Articulo art, Presupuesto pres)> SeedPresupuesto(
        AppDbContext db, int stock, int cantidad, decimal precio = 100m)
        {
            var art = new Articulo { Codigo = "A1", Descripcion = "Articulo", PrecioUnitario = precio, StockActual = stock, AlicuotaIva = 21m };
            db.Articulos.Add(art);

            var cliente = new Cliente { RazonSocial = "Cliente Test", Cuit = "20-00000000-0", CondicionIva = CondicionIva.ResponsableInscripto };
            db.Clientes.Add(cliente);
            await db.SaveChangesAsync();

            var pres = new Presupuesto
            {
                Numero = 1,
                Fecha = DateTime.UtcNow,
                ClienteId = cliente.Id,
                ValidezDias = 30,
                Estado = EstadoPresupuesto.Aprobado,
                Items = new List<PresupuestoItem>
            {
                new PresupuestoItem { ArticuloId = art.Id, Cantidad = cantidad, PrecioUnitario = precio, AlicuotaIva = 21m }
            }
            };
            db.Presupuestos.Add(pres);
            await db.SaveChangesAsync();

            return (art, pres);
        }

        [Fact]
        public async Task CrearPresupuesto_ConArticulosRepetidos_SumaCantidades()
        {
            var (_, db) = Armar();
            var numeracion = new NumeracionService(db);
            var presupuestos = new PresupuestoService(db, numeracion);

            var (art, pres) = await SeedPresupuesto(db, stock: 100, cantidad: 6);

            var nuevoPresupuesto = await presupuestos.CrearAsync(
                pres.ClienteId,
                30,
                new List<PresupuestoItem>
                {
            new() { ArticuloId = art.Id, Cantidad = 6 },
            new() { ArticuloId = art.Id, Cantidad = 4 }
                }
            );

            Assert.Single(nuevoPresupuesto.Items);
            Assert.Equal(10, nuevoPresupuesto.Items.First().Cantidad);
        }

        [Fact]
        public async Task CrearPresupuesto_SinItems_LanzaExcepcion()
        {
            var (_, db) = Armar();
            var numeracion = new NumeracionService(db);
            var presupuestos = new PresupuestoService(db, numeracion);

            var cliente = new Cliente
            {
                RazonSocial = "Cliente Test",
                Cuit = "20-00000000-0",
                CondicionIva = CondicionIva.ResponsableInscripto
            };

            db.Clientes.Add(cliente);
            await db.SaveChangesAsync();

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => presupuestos.CrearAsync(
                    cliente.Id,
                    30,
                    new List<PresupuestoItem>()
                )
            );
        }

        [Fact]
        public async Task CrearPresupuesto_CantidadCero_LanzaExcepcion()
        {
            var (_, db) = Armar();
            var numeracion = new NumeracionService(db);
            var presupuestos = new PresupuestoService(db, numeracion);

            var (art, _) = await SeedPresupuesto(db, stock: 100, cantidad: 1);

            var items = new List<PresupuestoItem>
            {
                new() { ArticuloId = art.Id, Cantidad = 0 }
            };

            var cliente = await db.Clientes.FirstAsync();

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => presupuestos.CrearAsync(cliente.Id, 30, items)
            );
        }
    }
}
