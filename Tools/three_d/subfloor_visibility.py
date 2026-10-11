"""Saved disposal visibility derived from the existing SubFloorHide tile owner."""
from __future__ import annotations

import math

PROTOTYPES = frozenset(('DisposalJunction', 'DisposalJunctionFlipped', 'DisposalXJunction'))


class SubfloorVisibility:
    def __init__(self, header, records, defaults, tile_definitions, transforms, decode_chunk):
        self.header, self.records, self.defaults = header, records, defaults
        self.definitions, self.transforms, self.decode_chunk = tile_definitions, transforms, decode_chunk
        self.grids = {}

    def check(self, uid):
        """Return visible/hidden/unknown without changing source records or gameplay tiles."""
        record = self.records[uid]
        default = self.defaults.get(record['prototype'], {})
        saved = record['components']
        if 'SubFloorHide' not in default and 'SubFloorHide' not in saved:
            return None, 'Missing SubFloorHide source owner', None
        owner = {**default.get('SubFloorHide', {}), **saved.get('SubFloorHide', {})}
        if owner.get('visibleLayers') not in (None, []):
            return None, 'SubFloorHide has independently visible layers', None
        try:
            anchored = self.transforms.local(uid).get('anchored', False)
            if type(anchored) is not bool:
                raise ValueError('Invalid anchored state')
            if not anchored:
                return True, 'Unanchored source is exposed', None
            grid_uid, visited = uid, set()
            while grid_uid in self.records and 'MapGrid' not in self.records[grid_uid]['components']:
                if grid_uid in visited:
                    raise ValueError('Cyclic grid parent')
                visited.add(grid_uid)
                grid_uid = int(self.transforms.local(grid_uid).get('parent', 0))
            if grid_uid not in self.records:
                return True, 'Anchored source without a grid is exposed', None
            grid = self.records[grid_uid]['components']['MapGrid']
            if grid.get('tileSize', 1) != 1:
                raise ValueError('Non-unit subfloor grid is unsupported')
            if grid_uid not in self.grids:
                tiles = {}
                for chunk in (grid.get('chunks', {}) or {}).values():
                    for tile in self.decode_chunk(chunk):
                        key = tile['x'], tile['y']
                        if key in tiles:
                            raise ValueError('Overlapping saved grid chunks')
                        tiles[key] = tile['palette']
                self.grids[grid_uid] = tiles
            gx, gy, yaw, _ = self.transforms.resolve(grid_uid)
            x, y, _, _ = self.transforms.resolve(uid)
            c, s = math.cos(yaw), math.sin(yaw)
            cell = math.floor(c*(x-gx)+s*(y-gy)), math.floor(-s*(x-gx)+c*(y-gy))
            index = self.grids[grid_uid].get(cell, 0)
            palette = self.header.get('tilemap', {})
            tile_id = palette.get(index, palette.get(str(index)))
            definition = self.definitions.get(tile_id, {})
            subfloor = definition.get('isSubfloor')
            if type(subfloor) is not bool:
                raise ValueError('Missing resolved tile isSubfloor metadata')
            return subfloor, 'Exposed subfloor tile' if subfloor else 'Covered by source floor tile', tile_id
        except (ValueError, TypeError, KeyError, OverflowError, AttributeError) as error:
            return None, str(error), None
