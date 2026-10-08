"""Read and write scenes in bounded JSON chunks so complete maps remain Git-portable."""
import json
import gzip
from pathlib import Path


def write_scene(scene, path):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    manifest = dict(scene)
    if scene.get('schemaVersion', 1) >= 2:
        directory = path.parent / path.stem
        directory.mkdir(exist_ok=True)
        chunks = []
        for name in ('instances', 'tiles', 'geometryVariants'):
            source = scene.get(name, {} if name == 'geometryVariants' else [])
            entries = list(source.items()) if isinstance(source, dict) else source
            batch_size = 1000 if isinstance(source, dict) else 20000
            manifest[name] = {} if isinstance(source, dict) else []
            for start in range(0, len(entries), batch_size):
                batch = entries[start:start+batch_size]
                payload = {name: dict(batch) if isinstance(source, dict) else batch}
                target = directory / f'{name}-{start//batch_size:03d}.json.gz'
                target.write_bytes(gzip.compress((json.dumps(payload, ensure_ascii=False, separators=(',', ':'))+'\n').encode('utf-8'), mtime=0))
                chunks.append(target.relative_to(path.parent).as_posix())
        manifest['sceneChunks'] = chunks
    path.write_text(json.dumps(manifest, ensure_ascii=False, separators=(',', ':'))+'\n', encoding='utf-8', newline='\n')


def read_scene(path):
    path = Path(path).resolve()
    scene = json.loads(path.read_text(encoding='utf-8'))
    for reference in scene.get('sceneChunks', []):
        target = (path.parent / reference).resolve()
        if not target.is_relative_to(path.parent):
            raise ValueError('Scene chunks must be local to the scene directory')
        payload = gzip.decompress(target.read_bytes()).decode('utf-8') if target.suffix == '.gz' else target.read_text(encoding='utf-8')
        for name, value in json.loads(payload).items():
            if name not in ('instances', 'tiles', 'geometryVariants'):
                raise ValueError('Unsupported scene chunk section')
            if isinstance(value, dict):
                scene.setdefault(name, {}).update(value)
            else:
                scene.setdefault(name, []).extend(value)
    return scene
