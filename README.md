# Jogo2D — TYPE DUEL
Jogos Digitais Insper 2026.2

Jogo versus de digitação em LAN: dois jogadores recebem o mesmo texto de 100 palavras e vence quem digitar tudo certo primeiro. Ao completar palavras o jogador ganha poderes (BOBOMB, GLITCH, CHANGE) para atrapalhar o adversário.

Detalhes de design e arquitetura em [visao_tecnica.md](visao_tecnica.md).

## Como jogar (MVP)

1. Um jogador clica em **HOST GAME** e passa o IP que aparece no lobby.
2. O outro digita esse IP e clica em **JOIN GAME**.
3. O host clica em **START MATCH**.
4. Digite o texto. Backspace apaga, setas ← → movem o cursor (uma posição por toque). **Botão esquerdo do mouse** usa o primeiro poder da fila.

Os dois computadores precisam estar na mesma rede (Wi-Fi/cabo) e a porta TCP **7777** liberada no firewall. Redes de campus podem isolar os dispositivos; se não conectar, use o hotspot do celular.

## Rodar / compilar

- Unity **6000.6.6f1**. Abra `My project/` e dê Play na `SampleScene` (a UI e o jogo são montados por código ao iniciar).
- Builds: menu **Tools > TypeDuel > Build macOS / Build Windows** (saída em `My project/Builds/`, ignorada pelo git).
- Teste sem digitar: `TypeDuel -td-auto host` e `TypeDuel -td-auto join -td-ip 127.0.0.1` (bot de desenvolvimento).

## Créditos

- Lista de palavras: palavras comuns em inglês, escolhidas pela equipe.
- Fonte padrão: Liberation Sans (OFL), incluída pelo TextMesh Pro.
