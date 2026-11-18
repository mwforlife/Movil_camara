# Sincronización manual de audio

En multiview, selecciona el teléfono y usa **Retardo de audio**: 0-1000 ms en pasos de 20 ms. Afecta a la fuente de audio independiente y a la salida A/V de ese teléfono. No modifica la cámara virtual ni añade retardo de video. El medidor muestra el nivel calculado antes del buffer de retardo, no el instante de reproducción en OBS.

1. En OBS, graba tres palmadas con las manos visibles. Revisa el archivo, evitando medir mediante la escucha del monitoreo de audio.
2. Si el sonido se adelanta a las manos, añade ese tiempo al retardo de audio. A 30 FPS, tres cuadros equivalen a 100 ms. Redondea al paso de 20 ms más próximo y verifica otra grabación.
3. Si el sonido llega después, deja este ajuste en 0 y aplica retardo al video de esa cámara en OBS. Aumentar el retardo de audio empeora ese caso.
4. Repite al inicio y al final de una grabación para distinguir desfase fijo de deriva. Registra teléfono, USB/Wi-Fi, resolución, FPS y desfase antes/después.
5. Recalibra tras cambiar transporte, calidad o fuente. Evita sumar una corrección de audio en OBS a otra en el receptor sin medir el resultado.

**Restablecer retardo** vuelve a 0 ms. Los valores se guardan por teléfono sólo mientras el receptor está abierto; se conservan en reconexiones dentro de ese proceso y se pierden al cerrar. No se mide ni calibra automáticamente.

Cada lector OBS tiene su propio buffer acotado (máximo 50 paquetes PCM). Se inicia con silencio, se limpia al cambiar el valor, al detener/reiniciar el audio o si el temporizador acumula más de 60 ms de atraso. Volumen y silencio se aplican después del buffer; silenciar no debe reproducir audio pendiente. Cambiar el retardo puede producir una discontinuidad breve: calibra antes de transmitir.

Este ajuste compensa desfase fijo cuando el audio se adelanta. No corrige cortes, latencia variable ni deriva sostenida. La validación física con los teléfonos y OBS sigue pendiente.
