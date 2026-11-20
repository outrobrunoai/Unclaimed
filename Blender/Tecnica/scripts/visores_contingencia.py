"""Display variants of the contingency panel (P02): one emission map per load total the puzzle can show, with the
same seven-segment painter and layout as Visor_Contingencia.png (TOTAL on the left window, LIMITE on the right).
python3 Blender/Tecnica/scripts/visores_contingencia.py
Totals: 6 (refrigeration only), 9 (with the general light), 10 (with the exam light) and 13 (the refused overload,
flashed by PainelDeContingencia)."""
import os, sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import etiquetas_tecnica as E

for total in (6, 9, 10, 13):
    E.visor(f'{E.TEX}/Visor_Contingencia_{total:02d}.png', (0.140, 0.032),
            [(0.004, 0.056, f'{total:>2}', None), (0.084, 0.136, '10', None)])
    print('visor', total)
