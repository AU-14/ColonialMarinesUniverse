#!/usr/bin/env python3
import argparse
import importlib.util
import pathlib
import re
import sys

import yamale
import yaml
from yamale.validators import DefaultValidators


try:
    BaseYamlLoader = yaml.CSafeLoader
except AttributeError:
    BaseYamlLoader = yaml.SafeLoader


class RobustYamlLoader(BaseYamlLoader):
    pass


def construct_unknown_tag(loader, _tag_suffix, node):
    if isinstance(node, yaml.MappingNode):
        return loader.construct_mapping(node)
    if isinstance(node, yaml.SequenceNode):
        return loader.construct_sequence(node)
    return loader.construct_scalar(node)


RobustYamlLoader.add_multi_constructor("!", construct_unknown_tag)


def load_validators(path: str | None):
    validators = DefaultValidators.copy()
    if not path:
        return validators

    validator_path = pathlib.Path(path)
    spec = importlib.util.spec_from_file_location("custom_validators", validator_path)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Unable to load validators from {validator_path}")

    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)

    for value in vars(module).values():
        tag = getattr(value, "tag", None)
        if isinstance(tag, str):
            validators[tag] = value

    return validators


def make_data(path: pathlib.Path):
    with path.open(encoding="utf-8") as file:
        documents = list(yaml.load_all(file, Loader=RobustYamlLoader))

    if not documents:
        return [({}, path.as_posix())]

    return [(document, path.as_posix()) for document in documents]


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--schema", required=True)
    parser.add_argument("--path-pattern", required=True)
    parser.add_argument("--validators")
    parser.add_argument("--root", default=".")
    args = parser.parse_args()

    root = pathlib.Path(args.root)
    pattern = re.compile(args.path_pattern)
    files = sorted(
        path for path in root.rglob("*")
        if path.is_file() and pattern.match(path.as_posix())
    )

    if not files:
        print(f"No files matched {args.path_pattern}; skipping.")
        return 0

    schema = yamale.make_schema(args.schema, validators=load_validators(args.validators))
    failures = 0

    for path in files:
        try:
            yamale.validate(schema, make_data(path))
            print(f"Validated {path.as_posix()}")
        except Exception as exc:
            failures += 1
            message = str(exc).replace("\n", "%0A")
            print(f"::error file={path.as_posix()}::{message}")

    return 1 if failures else 0


if __name__ == "__main__":
    sys.exit(main())
