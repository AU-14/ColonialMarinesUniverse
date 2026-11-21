"""CMU14: route feature changes, broadening through the source dependency graph."""

import re
from pathlib import PurePosixPath

TEST_ROOTS = ("Content.IntegrationTests/", "Content.CMU/IntegrationTests/")
UNIT_ROOTS = ("Content.Tests/", "Content.CMU/Tests/")
# Ignore declaration-shaped prose while discovering C# namespaces and types.
CS_NONCODE = re.compile(r'''//[^\n]*|/\*.*?\*/|("{3,}).*?\1|@"(?:[^"]|"")*"|"(?:\\.|[^"\\])*"|'(?:\\.|[^'\\])*' ''', re.S | re.X)


def runtime_feature(path):
    parts = PurePosixPath(path).parts
    if parts[0] == "Content.CMU" and len(parts) > 3 and parts[1] in ("Client", "Server", "Shared"):
        return "CMU14." + parts[2]
    if parts[0] in ("Content.Client", "Content.Server", "Content.Shared") and len(parts) > 2:
        if parts[1] in ("_CMU14", "CMU14", "_RMC14") and len(parts) > 3:
            return parts[1].lstrip("_") + "." + parts[2]
        if not parts[1].startswith("_"):
            return "Content.IntegrationTests.Tests." + parts[1]
    return None


def may_select(expression, namespace):
    """Whether a shard can contain a test under this namespace or fixture.

    Unknown method names or narrower fixture prefixes retain their shards.
    Exclusions still rule out whole features from catch-all groups.
    """
    namespace = namespace.rstrip(".") + "."
    tokens = re.findall(r"(?:FullyQualifiedName|Name)!?~[\w.]+|[()&|]", expression)
    if "".join(tokens) != re.sub(r"\s+", "", expression):
        return True
    index = 0

    def atom():
        nonlocal index
        token = tokens[index]
        index += 1
        if token == "(":
            value = expression_value()
            if index >= len(tokens) or tokens[index] != ")":
                raise ValueError("Unbalanced test filter")
            index += 1
            return value
        match = re.fullmatch(r"(FullyQualifiedName|Name)(!?~)([\w.]+)", token)
        if not match:
            raise ValueError("Invalid test filter")
        field, operation, prefix = match.groups()
        value = ({False, True} if field == "Name" else {True} if prefix in namespace
                 else {False, True} if namespace in prefix else {False})
        return {not item for item in value} if operation == "!~" else value

    def conjunction():
        nonlocal index
        value = atom()
        while index < len(tokens) and tokens[index] == "&":
            index += 1
            right = atom()
            value = {a and b for a in value for b in right}
        return value

    def expression_value():
        nonlocal index
        value = conjunction()
        while index < len(tokens) and tokens[index] == "|":
            index += 1
            right = conjunction()
            value = {a or b for a in value for b in right}
        return value

    try:
        result = expression_value()
        return True if index != len(tokens) else True in result
    except (ValueError, IndexError):
        return True


def documentation(path):
    return path.endswith(".md") and ("/" not in path or path.startswith(
        (".github/", "docs/", "Docs/", "Content.CMU/Docs/")))


def select_assets(paths):
    all_checks = paths is None or any(path == "RobustToolbox" or path == ".github/workflows/ci.yml"
                                      or path.startswith((".github/Schemas/", ".github/scripts/", "Tools/_CMU14/"))
                                      for path in paths)
    paths = paths or []
    checks = dict(rga=all_checks or any(re.search(r"attributions\.ya?ml$", path) for path in paths),
                  maps=all_checks or any(re.match(r"^(Content\.CMU/)?Resources/Maps/", path) for path in paths),
                  rsi=all_checks or any(".rsi/" in path for path in paths))
    checks["any"] = any(checks.values())
    return checks


def needs_unit(paths, files, impacted, complete):
    if not complete:
        return True
    if any(fixture.startswith(("Content.Tests.", "Content.CMU.Tests.")) for fixture in impacted):
        return True
    features = set()
    for path in paths:
        if documentation(path) or path.startswith(TEST_ROOTS):
            continue
        if path.startswith(UNIT_ROOTS) or not path.endswith(".cs") or path not in files:
            return True
        feature = runtime_feature(path)
        if feature is None:
            return True
        features.add(feature.removeprefix("Content.IntegrationTests.Tests."))
    # Keep the unit shard when it has any tests for a changed feature, including
    # tests reached dynamically without a direct reference to the changed type.
    return any(path.startswith(UNIT_ROOTS) and any(
        re.search(r"\." + re.escape(feature) + r"(?:\.|[;\s])", text)
        for feature in features) for path, text in files.items())


def select_shards(paths, shards, files, impacted, complete):
    """Unknown inputs and incomplete runtime dependency evidence keep all shards.

    A feature directory supplies the minimum selection even when its behavior is
    reached dynamically. The reverse graph adds consumers outside that feature.
    Shared infrastructure without a feature directory always runs the full suite.
    """
    all_names = {s["name"] for s in shards}
    def groups(namespace):
        return {s["name"] for s in shards if may_select(s["filter"], namespace)}

    relevant = [p for p in paths if not documentation(p)]
    if not relevant:
        return set()
    if not complete:
        # Fixtures can also expose helpers consumed by other fixtures. Without
        # the complete reverse graph, even a test-only edit cannot be narrowed.
        return all_names
    selected = set()
    for path in relevant:
        if path.startswith(TEST_ROOTS + UNIT_ROOTS):
            text = CS_NONCODE.sub(" ", files.get(path, ""))
            namespace = re.search(r"\bnamespace\s+([\w.]+)", text)
            classes = re.findall(r"\bclass\s+(\w+)", text)
            if namespace and classes and re.search(r"\[(?:Test|TestCase|TestCaseSource)\b", text):
                if path.startswith(UNIT_ROOTS) and complete:
                    # Pure unit fixtures need no gameplay run; reverse references
                    # still include integration consumers of fixture helpers.
                    continue
                matches = set().union(*(groups(namespace[1] + "." + name) for name in classes))
                if matches:
                    selected.update(matches)
                    continue
            # Includes deleted fixtures and changes to pool/setup/shared helpers.
            return all_names
        # Runtime assets, prototypes and build inputs can load arbitrary systems.
        # Keep full validation until their dependencies are explicitly modelled.
        if not path.endswith(".cs") or path not in files:
            return all_names
        domain = runtime_feature(path)
        if domain is None:
            return all_names
        matches = groups(domain)
        if not matches:
            return all_names
        selected.update(matches)
    for fixture in impacted:
        if fixture.startswith(("Content.Tests.", "Content.CMU.Tests.")):
            continue
        matches = groups(fixture)
        if not matches:
            return all_names
        selected.update(matches)
    return selected
