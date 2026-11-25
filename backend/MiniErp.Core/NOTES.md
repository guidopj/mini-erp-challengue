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