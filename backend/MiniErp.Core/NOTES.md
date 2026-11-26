### Bug encontrado: Cálculo del IVA

**Qué pasaba:**

El IVA se calculaba aplicando una única alícuota sobre el subtotal general. Esto generaba resultados incorrectos
cuando el presupuesto tenía artículos con distintas alícuotas (21% y 10,5%).

**Por qué pasaba:**

El cálculo no tenía en cuenta la alícuota individual de cada artículo. 
Además, las alícuotas estaban expresadas como porcentajes (`21` y `10.5`), por lo que era necesario dividirlas por 100
antes de utilizarlas en la operación.

**Cómo lo solucioné:**

Modifiqué el cálculo para obtener el IVA de cada artículo por separado, utilizando su propia alícuota. 
Primero se calcula el subtotal de cada línea aplicando el descuento y luego se calcula y redondea el IVA a dos decimales. 
Finalmente, se suman los subtotales y los importes de IVA de todas las líneas para obtener el total del presupuesto.

---

### Bug encontrado: Doble facturación

**Qué pasaba:**

Se podía facturar más de una vez el mismo presupuesto, provocando descuentos duplicados de stock y facturas repetidas.

**Por qué pasaba:**

Faltaba una validación para comprobar si el presupuesto ya había sido facturado antes de iniciar el proceso.

**Cómo lo solucioné:**

Agregué una condición para verificar el estado del presupuesto antes de facturarlo. 
Si ya está en estado `Facturado`, se lanza una excepción `InvalidOperationException` y se interrumpe el proceso.

### Bug encontrado: Stock negativo al facturar

**Qué pasaba:**

Se podía facturar un presupuesto aunque no hubiera stock suficiente, 
provocando que el stock de un artículo quedara en negativo.

**Por qué pasaba:**

Faltaba validar que el stock disponible fuera mayor o igual a la cantidad solicitada antes de realizar el descuento.

**Cómo lo solucioné:**

Separé el proceso en dos recorridos (foreach):

Primero valido que todos los artículos tengan stock suficiente.

Si todos pasan la validación, realizo el descuento de stock.

De esta manera, evito descuentos parciales y stock negativo.

¿Qué evito con esto?

Evito modificaciones parciales en las entidades que están siendo rastreadas por el Change Tracker de Entity Framework Core.
Aunque todavía no se haya ejecutado SaveChangesAsync(), los cambios realizados en las propiedades ya quedan registrados
en memoria por el contexto y pueden persistir o reutilizarse en consultas posteriores 
dentro del mismo DbContext, generando valores indeseados.

Al separar los recorridos, si falla una validación, no se modifica ningún stock.

### Bug encontrado: Artículos duplicados en un presupuesto

**Qué pasaba:**

Al agregar dos veces el mismo artículo a un presupuesto, se generaban dos ítems separados en lugar de sumar sus cantidades.

**Por qué pasaba:**

`CrearAsync()` recibía la lista de artículos y la asignaba directamente al presupuesto, sin agrupar los artículos repetidos.

**Cómo lo solucioné:**

Agregué un `GroupBy()` por `ArticuloId` para agrupar los artículos repetidos y un `Sum()` 
para acumular sus cantidades antes de crear el presupuesto.

**Test:**

Verifiqué que al agregar el mismo artículo con cantidades 6 y 4, se genere un único ítem con cantidad 10.

### Bug encontrado: Cantidad cero o negativa en los artículos de un presupuesto

**Qué pasaba:**

Se podían crear presupuestos con artículos cuya cantidad era cero o negativa.

**Por qué pasaba:**

`CrearAsync()` no validaba que la cantidad de los artículos fuera mayor a cero antes de crear el presupuesto.

**Cómo lo solucioné:**

Agregué una validación con `Any()` para comprobar que ningún artículo tenga una cantidad menor o igual a cero.

**Test:**

Verifiqué que al intentar crear un presupuesto con cantidad cero o negativa se lance una `InvalidOperationException`.

### Nueva funcionalidad: Duplicar presupuesto

**Qué se necesitaba:**

Permitir duplicar un presupuesto existente, generando uno nuevo sin modificar el original.

El presupuesto duplicado debe tener:
- Número nuevo.
- Fecha actual.
- Mismo cliente y plazo de validez.
- Mismos artículos, cantidades y descuentos.
- Precios unitarios y alícuotas de IVA actualizados según los valores vigentes de cada artículo.
- Estado inicial `Borrador`.

**Cómo lo solucioné:**

- Agregué el endpoint `POST /api/Presupuestos/{id}/duplicar` en `PresupuestosController`.
- Implementé `DuplicarAsync(int id)` en `PresupuestoService`.
- Recupero el presupuesto original con sus artículos.
- Creo nuevos ítems copiando `ArticuloId`, `Cantidad` y `DescuentoPct`.
- Reutilizo `CrearAsync()` para aprovechar las validaciones y la lógica existente de creación, numeración y actualización de precios.

**Test:**

- Dupliqué el presupuesto ID 2, que estaba en estado `Facturado`.
- Se generó correctamente el presupuesto ID 3, número 3.
- Se conservó el cliente, los artículos, las cantidades, los descuentos y la validez.
- El nuevo presupuesto quedó en estado `Borrador`.
- La respuesta del endpoint fue HTTP 200 OK.

### Validación de cliente al crear un presupuesto

**Qué pasaba:**

Se podía intentar crear un presupuesto utilizando un `ClienteId` que no existía en la base de datos.

**Por qué pasaba:**

El método `CrearAsync` no verificaba previamente la existencia del cliente.

**Cómo lo solucioné:**

Agregué una validación en `PresupuestoService.CrearAsync` utilizando `AnyAsync()` para comprobar que el cliente
exista antes de continuar con la creación.

Si el cliente no existe, se lanza una `InvalidOperationException` con el mensaje correspondiente.

**Tests realizados:**

- `CrearPresupuesto_ClienteInexistente_LanzaExcepcion`: verifica que se lance una excepción cuando el cliente no existe.
- `CrearPresupuesto_ClienteExistente_AsociaCliente`: verifica que el presupuesto quede asociado al cliente existente.


**Decisiones del cliente React (estructura, librerías, cómo calculás los totales en vivo).**

Para el frontend usé Next.js, React y TypeScript. Para la interfaz utilicé Material UI (MUI), 
que ofrece componentes listos para usar, como tablas, formularios, botones y alertas.

Organicé el código separando los componentes, hooks, servicios, tipos y funciones auxiliares. 
La idea fue mantener cada parte ordenada, facilitar su mantenimiento y evitar repetir código.

Los totales se actualizan en vivo cuando agrego un artículo o modifico su cantidad o descuento. 
Se calcula el subtotal de cada artículo, se aplica el descuento y luego se calcula el IVA según la alícuota 
correspondiente. Finalmente, se suman los importes para mostrar el total del presupuesto.

También agregué tests para comprobar que los componentes y la lógica funcionen correctamente.

**Qué hiciste y qué dejaste afuera, y cómo priorizaste.**

solo dejé afuera lo de los reportes porque no eran mandatorios y prioricé entregarlo con aire en cuanto a tiempo.
Hice todo el resto.

**Cómo usaste IA: para qué partes, qué le pediste, qué aceptaste y qué descartaste, 
y dónde se equivocó. (Usar IA está permitido y lo valoramos. Lo que evaluamos es tu criterio al usarla.)**

la IA me ayudó mayormente con el cálculo de los totales en vivo y los tests mas que nada en front ya que en back eran
bastante repetitivos y al no arrancar de cero tenia de donde aferrarme. No se equivocaba pero si retornaba una solución o fix
no acorde con lo que el proyecto necesitaba

**Qué harías con más tiempo / qué le falta para producción.**

del lado del front end soy de usar react-query, que es una librería que ayuda mucho para ser mas minucioso a la hora de
los requests, además retorna propiedades de loading / success / failure y evita crear useState para manejar cada estado del
request y maneja cache lo cual le agrega un componente de optimización el cuál no consideramos para esta tarea.

del lado del back, no manejamos concurrencia, podría pasar que pedidos concurrentes violen algunas de las normas de negocio
por no usar transacciones algún otro tipo de manejo de varios procesos intentando modificar o aplicar algún cambio a la vez
