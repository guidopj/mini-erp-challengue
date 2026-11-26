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

        [Fact]
        public async Task DuplicarPresupuesto_ActualizaPrecios_YConservaDatosOriginales()
        {
            /*
             Comprueba que al duplicar un presupuesto se genera uno nuevo, 
             se conservan los datos originales (cliente, cantidades y descuentos), 
             se actualizan los precios y las alícuotas de IVA según los valores vigentes de los artículos 
             y el presupuesto original permanece sin modificaciones.
             */
            var (_, db) = Armar();
            var numeracion = new NumeracionService(db);
            var presupuestos = new PresupuestoService(db, numeracion);

            var (art, original) = await SeedPresupuesto(
                db,
                stock: 100,
                cantidad: 3,
                precio: 100m
            );

            // Configuramos descuento en el presupuesto original.
            original.Items.First().DescuentoPct = 15m;
            await db.SaveChangesAsync();

            // Actualizamos el precio y el IVA del artículo.
            art.PrecioUnitario = 120m;
            art.AlicuotaIva = 10m;
            await db.SaveChangesAsync();

            var duplicado = await presupuestos.DuplicarAsync(original.Id);

            // Nuevo presupuesto.
            Assert.NotEqual(original.Id, duplicado.Id);
            Assert.Equal(2, duplicado.Numero);
            Assert.Equal(EstadoPresupuesto.Borrador, duplicado.Estado);
            Assert.Equal(original.ClienteId, duplicado.ClienteId);
            Assert.Equal(original.ValidezDias, duplicado.ValidezDias);

            // Fecha actual.
            Assert.True(
                (DateTime.UtcNow - duplicado.Fecha).Duration() < TimeSpan.FromMinutes(1)
            );

            // Datos de los artículos.
            var itemOriginal = original.Items.Single();
            var itemDuplicado = duplicado.Items.Single();

            Assert.NotEqual(itemOriginal.Id, itemDuplicado.Id);
            Assert.Equal(itemOriginal.ArticuloId, itemDuplicado.ArticuloId);
            Assert.Equal(3, itemDuplicado.Cantidad);
            Assert.Equal(15m, itemDuplicado.DescuentoPct);

            // Precios y alícuotas actualizados.
            Assert.Equal(100m, itemOriginal.PrecioUnitario);
            Assert.Equal(21m, itemOriginal.AlicuotaIva);

            Assert.Equal(120m, itemDuplicado.PrecioUnitario);
            Assert.Equal(10m, itemDuplicado.AlicuotaIva);
        }

        [Fact]
        public async Task CrearPresupuesto_ClienteInexistente_LanzaExcepcion()
        {
            var (_, db) = Armar();
            var numeracion = new NumeracionService(db);
            var presupuestos = new PresupuestoService(db, numeracion);

            var items = new List<PresupuestoItem>
    {
        new() { ArticuloId = 1, Cantidad = 1 }
    };

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => presupuestos.CrearAsync(999, 30, items)
            );

            Assert.Equal("El cliente 999 no existe.", exception.Message);
        }

        [Fact]
        public async Task CrearPresupuesto_ClienteExistente_AsociaCliente()
        {
            var (_, db) = Armar();
            var presupuestos = new PresupuestoService(db, new NumeracionService(db));

            var (art, _) = await SeedPresupuesto(db, stock: 100, cantidad: 1);

            var cliente = await db.Clientes.FirstAsync();

            var resultado = await presupuestos.CrearAsync(
                cliente.Id,
                30,
                new List<PresupuestoItem>
                {
            new() { ArticuloId = art.Id, Cantidad = 1 }
                }
            );

            Assert.Equal(cliente.Id, resultado.ClienteId);
        }
    }
}
