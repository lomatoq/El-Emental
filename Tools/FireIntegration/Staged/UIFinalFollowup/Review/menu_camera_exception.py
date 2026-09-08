"""Narrow opt-in for verify_graphics_preservation.py after visual acceptance only."""
def approved_menu_orbit_only(key, previous, current):
    if key != '1968201427' or previous[0] != '114' or current[0] != '114':
        return False
    old, new = previous[1], current[1]
    script = '  m_Script: {fileID: 11500000, guid: 8310ec71fcbc56943bc2c082548b525c, type: 3}\n'
    old_line, new_line = '  presentationAzimuth: 35\n', '  presentationAzimuth: 0\n'
    return (script in old and script in new and old.count(old_line) == 1
            and new.count(new_line) == 1 and new.replace(new_line, old_line, 1) == old)

if __name__ == '__main__':
    script='  m_Script: {fileID: 11500000, guid: 8310ec71fcbc56943bc2c082548b525c, type: 3}\n'
    old=script+'  fieldOfView: 38\n  presentationAzimuth: 35\n  countdownAzimuth: 85\n'
    new=old.replace('presentationAzimuth: 35','presentationAzimuth: 0')
    assert approved_menu_orbit_only('1968201427',('114',old),('114',new))
    assert not approved_menu_orbit_only('other',('114',old),('114',new))
    assert not approved_menu_orbit_only('1968201427',('114',old),('114',new.replace('fieldOfView: 38','fieldOfView: 39')))
    assert not approved_menu_orbit_only('1968201427',('114',old),('114',new.replace('countdownAzimuth: 85','countdownAzimuth: 0')))
    assert not approved_menu_orbit_only('1968201427',('114',old),('114',new.replace('8310ec71','00000000')))
    assert not approved_menu_orbit_only('1968201427',('114',new),('114',new))
    print('6 narrow menu-orbit preservation checks passed')
