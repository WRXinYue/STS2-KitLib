import re
import sys


def convert_inline(text):
    text = re.sub(r"~~(.*?)~~", r"[strike]\1[/strike]", text)
    text = re.sub(r"\*\*(.+?)\*\*", r"[b]\1[/b]", text)
    text = re.sub(r"__(.+?)__", r"[b]\1[/b]", text)
    text = re.sub(r"`([^`]+)`", r"[code]\1[/code]", text)
    text = re.sub(r"(?<!\*)\*(?!\*)(.+?)(?<!\*)\*(?!\*)", r"[i]\1[/i]", text)
    text = re.sub(r"(?<!_)_(?!_)(.+?)(?<!_)_(?!_)", r"[i]\1[/i]", text)
    text = re.sub(r"\[([^\]]+)\]\(([^)]+)\)", r"[url=\2]\1[/url]", text)
    return text


def convert_markdown(lines):
    output = []
    in_code = False
    in_list = False
    in_olist = False
    in_quote = False

    def close_lists():
        nonlocal in_list, in_olist
        if in_list:
            output.append("[/list]")
            in_list = False
        if in_olist:
            output.append("[/olist]")
            in_olist = False

    def close_quote():
        nonlocal in_quote
        if in_quote:
            output.append("[/quote]")
            in_quote = False

    for raw in lines:
        line = raw.rstrip("\n")
        stripped = line.strip()

        if stripped.startswith("```"):
            close_lists()
            close_quote()
            if in_code:
                output.append("[/code]")
                in_code = False
            else:
                output.append("[code]")
                in_code = True
            continue

        if in_code:
            output.append(line)
            continue

        if re.match(r"^\s*[-*_]\s*[-*_]\s*[-*_]\s*$", stripped):
            close_lists()
            close_quote()
            output.append("[hr][/hr]")
            continue

        if stripped.startswith(">"):
            close_lists()
            if not in_quote:
                output.append("[quote]")
                in_quote = True
            content = stripped.lstrip(">").lstrip()
            output.append(convert_inline(content))
            continue
        if in_quote and stripped == "":
            close_quote()

        if stripped == "":
            close_lists()
            close_quote()
            output.append("")
            continue

        m = re.match(r"^(#{1,3})\s+(.+)$", stripped)
        if m:
            close_lists()
            close_quote()
            level = len(m.group(1))
            output.append(f"[h{level}]{convert_inline(m.group(2))}[/h{level}]")
            continue

        m = re.match(r"^\s*(\d+)\.\s+(.+)$", line)
        if m:
            close_quote()
            if not in_olist:
                close_lists()
                output.append("[olist]")
                in_olist = True
            output.append(f"[*]{convert_inline(m.group(2))}")
            continue

        m = re.match(r"^\s*[-*+]\s+(.+)$", line)
        if m:
            close_quote()
            if not in_list:
                close_lists()
                output.append("[list]")
                in_list = True
            output.append(f"[*]{convert_inline(m.group(1))}")
            continue

        close_lists()
        close_quote()
        output.append(convert_inline(line))

    close_lists()
    close_quote()
    if in_code:
        output.append("[/code]")

    return "\n".join(output).strip()


def main():
    text = sys.stdin.read()
    if not text:
        return
    lines = text.splitlines()
    result = convert_markdown(lines)
    sys.stdout.write(result)


if __name__ == "__main__":
    main()
