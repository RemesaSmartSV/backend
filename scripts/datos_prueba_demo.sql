--Antes de ejecutar todo este script, es necesario primero ingresar un hogar para que este no crashee
--Aqui dejo el dato de prueba para quien vea esto y ejecutelo antes que todo el script, para que no de error al insertar los datos de prueba:
--INSERT INTO "Hogares" ("IdHogar", "Nombre", "FechaCreacion") 
--VALUES (1, 'Hogar Mendoza', CURRENT_TIMESTAMP);

--Una vez insertado el hogar, se puede ejecutar todo el script de datos de prueba para poblar la base de datos con información inicial.

-- 1. Insertar Usuarios
INSERT INTO "Usuarios" ("IdUsuario", "IdHogar", "Nombre", "Correo", "ContrasenaHash", "Rol", "FechaRegistro") 
VALUES 
(1, 1, 'Carlos Mendoza', 'carlos.mendoza@email.com', '$2a$11$N9xlhjW2r4B/Zq9r...', 'Administrador', '2026-09-01 08:30:00'),
(2, 1, 'Ana Trujillo', 'ana.trujillo@email.com', '$2a$11$J8xkpoW3t5C/Yx8p...', 'Miembro', '2026-09-02 10:15:00');

-- 2. Insertar Categorías
INSERT INTO "Categorias" ("IdCategoria", "IdHogar", "Nombre", "Tipo", "Icono") 
VALUES 
(1, 1, 'Remesas', 'Ingreso', '💸'),
(2, 1, 'Alimentación', 'Gasto', '🛒'),
(3, 1, 'Servicios Básicos', 'Gasto', '💡'),
(4, 1, 'Salud', 'Gasto', '🏥');

-- 3. Insertar Tips Financieros
INSERT INTO "TipsFinancieros" ("IdTip", "IdCategoria", "Titulo", "Contenido") 
VALUES 
(1, 2, 'Planificación de menú semanal', 'Elaborar un menú semanal antes de ir al supermercado te ayuda a comprar solo lo necesario y evitar desperdicios, reduciendo significativamente tus gastos mensuales.'),
(2, 3, 'Optimización del consumo eléctrico', 'Desconectar los electrodomésticos que no están en uso y aprovechar la luz natural puede disminuir hasta un 15% el monto de tu recibo de energía mensual.');

-- 4. Insertar Presupuestos
INSERT INTO "Presupuestos" ("IdPresupuesto", "IdHogar", "IdCategoria", "MontoLimite", "MesAnio") 
VALUES 
(1, 1, 2, 300.00, '2026-09-01 00:00:00'),
(2, 1, 3, 80.00, '2026-09-01 00:00:00'),
(3, 1, 4, 150.00, '2026-09-01 00:00:00');

-- 5. Insertar Movimientos
INSERT INTO "Movimientos" ("IdMovimiento", "IdHogar", "IdUsuario", "IdCategoria", "Monto", "Fecha", "Tipo", "Descripcion", "OrigenEmisora") 
VALUES 
(1, 1, 1, 1, 500.00, '2026-09-05 10:30:00', 'Ingreso', 'Remesa mensual de familiares', 'Western Union'),
(2, 1, 2, 2, 125.75, '2026-09-10 16:45:00', 'Gasto', 'Compra de quincena en supermercado', 'Super Selectos'),
(3, 1, 1, 3, 34.50, '2026-09-14 09:20:00', 'Gasto', 'Pago de recibo de electricidad de agosto', 'AES El Salvador'),
(4, 1, 2, 4, 45.00, '2026-09-20 11:15:00', 'Gasto', 'Consulta médica general y medicamentos', 'Farmacias Económicas');