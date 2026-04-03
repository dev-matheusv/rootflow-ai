import { Info } from "lucide-react";

import { cn } from "@/lib/utils";

type FormattedAnswerProps = {
  content: string;
  className?: string;
};

type Block =
  | { type: "heading"; content: string }
  | { type: "paragraph"; content: string }
  | { type: "list"; title?: string; items: string[] }
  | { type: "note"; label: string; content: string };

function stripFormatting(value: string) {
  return value.replace(/\*\*(.+?)\*\*/g, "$1").replace(/`(.+?)`/g, "$1").trim();
}

function normalizeForDetection(value: string) {
  return value
    .normalize("NFD")
    .replace(/\p{Diacritic}/gu, "")
    .toLowerCase();
}

function isSeparator(line: string) {
  return /^(-{3,}|_{3,}|\*{3,})$/.test(line.trim());
}

function parseBullet(line: string) {
  const match = line.match(/^\s*[-*\u2022]\s+(.+)$/u);
  return match ? stripFormatting(match[1]) : null;
}

function parseNote(line: string) {
  const trimmed = stripFormatting(line);
  const normalized = normalizeForDetection(trimmed);
  const prefixes = ["optional note:", "note:", "nota:", "observacao:"];
  const prefix = prefixes.find((candidate) => normalized.startsWith(candidate));

  if (!prefix) {
    return null;
  }

  const label = prefix.endsWith(":") ? prefix.slice(0, -1) : prefix;
  const content = trimmed.slice(trimmed.indexOf(":") + 1).trim();

  return {
    label: label
      .split(" ")
      .map((part) => part.charAt(0).toUpperCase() + part.slice(1))
      .join(" "),
    content,
  };
}

function isHeading(line: string) {
  const trimmed = stripFormatting(line);

  if (!trimmed || trimmed.length > 88) {
    return false;
  }

  if (/^\*\*.+\*\*$/.test(line.trim())) {
    return true;
  }

  if (!trimmed.endsWith(":")) {
    return false;
  }

  const plain = trimmed.slice(0, -1);
  if (!plain || /[.!?]/.test(plain)) {
    return false;
  }

  return plain.split(/\s+/).length <= 8;
}

function collectBulletItems(lines: string[], startIndex: number) {
  const items: string[] = [];
  let index = startIndex;

  while (index < lines.length) {
    const rawLine = lines[index];
    const trimmed = rawLine.trim();

    if (!trimmed || isSeparator(trimmed)) {
      if (items.length) {
        index += 1;
        break;
      }

      index += 1;
      continue;
    }

    if ((parseNote(trimmed) || isHeading(trimmed)) && items.length) {
      break;
    }

    const bullet = parseBullet(rawLine);
    if (bullet) {
      items.push(bullet);
      index += 1;
      continue;
    }

    if (items.length) {
      items[items.length - 1] = `${items[items.length - 1]} ${stripFormatting(trimmed)}`.trim();
      index += 1;
      continue;
    }

    break;
  }

  return { items, nextIndex: index };
}

function parseBlocks(content: string): Block[] {
  const lines = content.replace(/\r\n/g, "\n").split("\n");
  const blocks: Block[] = [];

  let index = 0;
  while (index < lines.length) {
    const trimmed = lines[index].trim();

    if (!trimmed || isSeparator(trimmed)) {
      index += 1;
      continue;
    }

    const note = parseNote(trimmed);
    if (note) {
      blocks.push({ type: "note", ...note });
      index += 1;
      continue;
    }

    if (isHeading(trimmed)) {
      const title = stripFormatting(trimmed).replace(/:$/, "");
      let lookahead = index + 1;

      while (lookahead < lines.length && !lines[lookahead].trim()) {
        lookahead += 1;
      }

      if (parseBullet(lines[lookahead] ?? "")) {
        const { items, nextIndex } = collectBulletItems(lines, lookahead);
        if (items.length) {
          blocks.push({ type: "list", title, items });
          index = nextIndex;
          continue;
        }
      }

      blocks.push({ type: "heading", content: title });
      index += 1;
      continue;
    }

    const bullet = parseBullet(lines[index]);
    if (bullet) {
      if (bullet.endsWith(":")) {
        const title = bullet.slice(0, -1).trim();
        const { items, nextIndex } = collectBulletItems(lines, index + 1);

        if (items.length) {
          blocks.push({ type: "list", title, items });
          index = nextIndex;
          continue;
        }
      }

      const { items, nextIndex } = collectBulletItems(lines, index);
      blocks.push({ type: "list", items });
      index = nextIndex;
      continue;
    }

    const paragraphLines = [stripFormatting(trimmed)];
    index += 1;

    while (index < lines.length) {
      const nextLine = lines[index].trim();

      if (!nextLine || isSeparator(nextLine)) {
        index += 1;
        break;
      }

      if (parseNote(nextLine) || isHeading(nextLine) || parseBullet(lines[index])) {
        break;
      }

      paragraphLines.push(stripFormatting(nextLine));
      index += 1;
    }

    blocks.push({ type: "paragraph", content: paragraphLines.join(" ") });
  }

  return blocks;
}

function getBlockKey(block: Block, index: number) {
  switch (block.type) {
    case "heading":
    case "paragraph":
      return `${block.type}:${block.content}`;
    case "list":
      return `${block.type}:${block.title ?? "untitled"}:${block.items.join("|")}`;
    case "note":
      return `${block.type}:${block.label}:${block.content}`;
    default:
      return `${index}`;
  }
}

export function FormattedAnswer({ content, className }: FormattedAnswerProps) {
  const blocks = parseBlocks(content);

  return (
    <div className={cn("space-y-4 text-foreground", className)}>
      {blocks.map((block, index) => {
        const blockKey = getBlockKey(block, index);

        switch (block.type) {
          case "heading":
            return (
              <div
                key={blockKey}
                className="text-[11px] font-semibold uppercase tracking-[0.18em] text-primary/78"
              >
                {block.content}
              </div>
            );

          case "paragraph":
            return (
              <p key={blockKey} className="text-[0.98rem] leading-7 text-foreground/94">
                {block.content}
              </p>
            );

          case "list":
            return (
              <section key={blockKey} className="space-y-2.5 rounded-[18px] border border-border/60 bg-background/60 p-4">
                {block.title ? (
                  <div className="text-sm font-semibold tracking-[-0.01em] text-foreground">{block.title}</div>
                ) : null}
                <ul className="space-y-2.5">
                  {block.items.map((item, itemIndex) => (
                    <li key={`${blockKey}:${itemIndex}:${item}`} className="flex items-start gap-3">
                      <span className="mt-[0.62rem] size-1.5 rounded-full bg-primary/80" />
                      <span className="text-[0.96rem] leading-7 text-foreground/90">{item}</span>
                    </li>
                  ))}
                </ul>
              </section>
            );

          case "note":
            return (
              <aside
                key={blockKey}
                className="flex gap-3 border-l-2 border-primary/24 pl-4"
              >
                <div className="mt-0.5 flex size-8 shrink-0 items-center justify-center rounded-2xl bg-primary/8 text-primary">
                  <Info className="size-4" />
                </div>
                <div className="space-y-1">
                  <div className="text-sm font-semibold text-foreground">{block.label}</div>
                  <p className="text-sm leading-6 text-foreground/84">{block.content}</p>
                </div>
              </aside>
            );
        }
      })}
    </div>
  );
}
