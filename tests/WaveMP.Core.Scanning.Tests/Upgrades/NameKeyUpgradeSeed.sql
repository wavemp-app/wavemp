-- A realistic library on the schema before the migration ReferenceGenresByIdAndNormalizeNames (after
-- 20261126093256_AddAlbumCoverFallback), as the scanner stored it then: names compared in upper case, so spellings
-- that differ in punctuation, spaces or diacritics are separate artists, albums and genres. Three profiles with
-- favorites, ratings, history, playlists, smart playlists, cover rules, artist pictures, online covers and players.
-- Ids are derived from the names with md5, so the tests can name the entries.

-- Artists: pairs that the normalized names join, and names that stay apart.
INSERT INTO artists (id, name, normalized_name, first_seen_at, music_brainz_artist_id)
SELECT md5('artist:' || a.name)::uuid, a.name, upper(a.name),
    timestamptz '2025-01-01 00:00:00+00' + make_interval(days => a.day), a.mbid
FROM (VALUES
    ('K.Flay', 1, NULL::uuid),
    ('K Flay', 5, '5b11f4ce-a62d-471e-81fc-a69a8278c7da'::uuid),
    ('K/DA', 2, NULL),
    ('KDA', 9, NULL),
    ('AC/DC', 3, NULL),
    ('AC-DC', 4, NULL),
    ('Beyoncé', 2, NULL),
    ('Beyonce', 8, NULL),
    ('Guns N'' Roses', 3, NULL),
    ('Guns N Roses', 6, NULL),
    ('Simon & Garfunkel', 2, NULL),
    ('Simon and Garfunkel', 3, NULL),
    ('Sigur Rós', 1, NULL),
    ('Sigur Ros', 2, NULL),
    ('Mötley Crüe', 1, NULL),
    ('Motley Crue', 1, NULL),
    ('Calvin Harris', 1, NULL),
    ('Rihanna', 1, NULL),
    ('Calvin Harris feat. Rihanna', 2, NULL),
    ('Calvin Harris ft. Rihanna', 3, NULL),
    ('Major Lazer x DJ Snake', 1, NULL),
    ('Major Lazer & DJ Snake', 2, NULL),
    ('Various Artists', 0, NULL),
    ('坂本龍一', 1, NULL),
    ('Мумий Тролль', 1, NULL),
    ('!!!', 1, NULL),
    ('Prince', 1, NULL)
) AS a(name, day, mbid);

INSERT INTO artists (id, name, normalized_name, first_seen_at)
SELECT md5('artist:Band ' || n)::uuid, 'Band ' || n, 'BAND ' || n,
    timestamptz '2025-02-01 00:00:00+00' + make_interval(days => n)
FROM generate_series(1, 25) AS n;

-- Albums by artist, with the keys of the scanner of that time.
CREATE TEMPORARY TABLE seed_albums (artist text, title text, compilation boolean, day integer, priority text,
    fallback text, folder text);
INSERT INTO seed_albums VALUES
    ('K.Flay', 'Solutions', false, 1, NULL, NULL, NULL),
    ('K Flay', 'Solutions', false, 5, 'TrackFirst', 'None', NULL),
    ('K Flay', 'Every Where Is Some Where', false, 6, NULL, NULL, NULL),
    ('K/DA', 'All Out', false, 2, NULL, NULL, NULL),
    ('KDA', 'ALL OUT', false, 9, NULL, NULL, NULL),
    ('AC/DC', 'Back in Black', false, 3, NULL, NULL, NULL),
    ('AC-DC', 'Back In Black', false, 4, 'TrackOnly', NULL, NULL),
    ('Beyoncé', 'Lemonade', false, 2, NULL, NULL, NULL),
    ('Beyonce', 'Lemonade', false, 8, NULL, 'None', NULL),
    ('Beyonce', 'Renaissance', false, 8, NULL, NULL, NULL),
    ('Guns N'' Roses', 'Appetite for Destruction', false, 3, NULL, NULL, NULL),
    ('Guns N Roses', 'Appetite For Destruction', false, 6, NULL, NULL, NULL),
    ('Simon & Garfunkel', 'Bridge over Troubled Water', false, 2, NULL, NULL, NULL),
    ('Simon & Garfunkel', 'Greatest Hits', false, 2, NULL, NULL, NULL),
    ('Simon and Garfunkel', 'Greatest Hits', false, 3, NULL, NULL, NULL),
    ('Sigur Rós', 'Ágætis byrjun', false, 1, NULL, NULL, NULL),
    ('Sigur Ros', 'Agaetis byrjun', false, 2, NULL, NULL, NULL),
    ('Mötley Crüe', 'Dr. Feelgood', false, 1, NULL, NULL, NULL),
    ('Motley Crue', 'Dr Feelgood', false, 1, NULL, NULL, NULL),
    ('Calvin Harris', '18 Months', false, 1, NULL, NULL, NULL),
    ('Various Artists', 'Now 100', true, 1, NULL, NULL, NULL),
    ('坂本龍一', 'Async', false, 1, NULL, NULL, NULL),
    ('Мумий Тролль', 'Морская', false, 1, NULL, NULL, NULL),
    ('!!!', 'Wallop', false, 1, NULL, NULL, NULL),
    ('Prince', 'Purple Rain', false, 1, NULL, NULL, NULL),
    (NULL, 'Mix', false, 1, NULL, NULL, 'Mixes/One'),
    (NULL, 'MIX!', false, 2, NULL, NULL, 'Mixes/One'),
    (NULL, 'Mix', false, 3, NULL, NULL, 'Mixes/Two');
INSERT INTO seed_albums
SELECT 'Band ' || n, 'Album ' || n, false, 10 + n, NULL, NULL, NULL FROM generate_series(1, 25) AS n;

INSERT INTO albums (id, title, normalized_title, album_key, album_artist_id, is_compilation, year, first_seen_at,
    updated_at, cover_priority, cover_fallback)
SELECT md5('album:' || coalesce(s.artist, s.folder) || '|' || s.title)::uuid, s.title, upper(s.title),
    encode(sha256(convert_to(
        CASE WHEN s.artist IS NULL
            THEN 'folder:019a0000-0000-7000-8000-000000000001/' || lower(s.folder)
            ELSE 'artist:' || upper(s.artist)
        END || '|' || upper(s.title), 'UTF8')), 'hex'),
    CASE WHEN s.artist IS NULL THEN NULL ELSE md5('artist:' || s.artist)::uuid END,
    s.compilation, 2000 + s.day, timestamptz '2025-01-01 00:00:00+00' + make_interval(days => s.day),
    timestamptz '2025-06-01 00:00:00+00', s.priority, s.fallback
FROM seed_albums AS s;

-- Tracks: album artist (or folder), album, track artist, number of songs, genre as tagged.
CREATE TEMPORARY TABLE seed_tracks (artist text, album text, track_artist text, songs integer, genre text,
    folder text);
INSERT INTO seed_tracks VALUES
    ('K.Flay', 'Solutions', 'K.Flay', 4, 'Hip-Hop', NULL),
    ('K Flay', 'Solutions', 'K Flay', 2, 'hip hop', NULL),
    ('K Flay', 'Every Where Is Some Where', 'K Flay', 3, 'Alternative', NULL),
    ('K/DA', 'All Out', 'K/DA', 3, 'K-Pop', NULL),
    ('KDA', 'ALL OUT', 'KDA', 2, 'KPop', NULL),
    ('AC/DC', 'Back in Black', 'AC/DC', 4, 'Rock', NULL),
    ('AC-DC', 'Back In Black', 'AC-DC', 2, 'rock', NULL),
    ('Beyoncé', 'Lemonade', 'Beyoncé', 4, 'R&B', NULL),
    ('Beyonce', 'Lemonade', 'Beyonce', 2, 'R & B', NULL),
    ('Beyonce', 'Renaissance', 'Beyonce', 3, 'Dance', NULL),
    ('Guns N'' Roses', 'Appetite for Destruction', 'Guns N'' Roses', 3, 'Hard Rock', NULL),
    ('Guns N Roses', 'Appetite For Destruction', 'Guns N Roses', 2, 'Hard-Rock', NULL),
    ('Simon & Garfunkel', 'Bridge over Troubled Water', 'Simon & Garfunkel', 3, 'Folk', NULL),
    ('Simon & Garfunkel', 'Greatest Hits', 'Simon & Garfunkel', 2, 'Folk', NULL),
    ('Simon and Garfunkel', 'Greatest Hits', 'Simon and Garfunkel', 2, ' folk ', NULL),
    ('Sigur Rós', 'Ágætis byrjun', 'Sigur Rós', 3, 'Post-Rock', NULL),
    ('Sigur Ros', 'Agaetis byrjun', 'Sigur Ros', 2, 'Post Rock', NULL),
    ('Mötley Crüe', 'Dr. Feelgood', 'Mötley Crüe', 2, 'Metal', NULL),
    ('Motley Crue', 'Dr Feelgood', 'Motley Crue', 2, 'metal', NULL),
    ('Calvin Harris', '18 Months', 'Calvin Harris feat. Rihanna', 1, 'Dance', NULL),
    ('Calvin Harris', '18 Months', 'Calvin Harris ft. Rihanna', 1, 'Dance', NULL),
    ('Calvin Harris', '18 Months', 'Calvin Harris', 3, 'EDM', NULL),
    ('Various Artists', 'Now 100', 'Major Lazer x DJ Snake', 1, 'Dance', NULL),
    ('Various Artists', 'Now 100', 'Major Lazer & DJ Snake', 1, 'Dance', NULL),
    ('Various Artists', 'Now 100', 'Rihanna', 1, 'Pop', NULL),
    ('Various Artists', 'Now 100', 'Beyonce', 1, 'Pop', NULL),
    ('Various Artists', 'Now 100', 'K Flay', 1, 'pop', NULL),
    ('坂本龍一', 'Async', '坂本龍一', 2, 'Ambient', NULL),
    ('Мумий Тролль', 'Морская', 'Мумий Тролль', 2, 'Рок', NULL),
    ('!!!', 'Wallop', '!!!', 2, 'Dance-Punk', NULL),
    ('Prince', 'Purple Rain', 'Prince', 3, 'Funk', NULL),
    (NULL, 'Mix', NULL, 2, 'Electronic', 'Mixes/One'),
    (NULL, 'MIX!', NULL, 2, 'electronic', 'Mixes/One'),
    (NULL, 'Mix', NULL, 2, NULL, 'Mixes/Two'),
    (NULL, NULL, 'Prince', 2, NULL, 'Singles');
INSERT INTO seed_tracks
SELECT 'Band ' || n, 'Album ' || n, 'Band ' || n, 4,
    (ARRAY['Rock', 'Pop', 'Jazz', 'Electronic', 'Indie'])[1 + n % 5], NULL
FROM generate_series(1, 25) AS n;

INSERT INTO tracks (id, directory_path, base_name, normalized_base_name, title, artist_id, album_id, track_number,
    disc_number, year, genre, duration, has_audio, has_video, metadata_source, first_seen_at, updated_at,
    video_offset_milliseconds, source_id)
SELECT md5('track:' || t.directory || '|' || t.base_name)::uuid, t.directory, t.base_name, upper(t.base_name),
    'Song ' || t.number || ' of ' || coalesce(t.album, t.track_artist), t.artist_id, t.album_id, t.number, 1, 2010,
    t.genre, make_interval(secs => 180 + t.number * 7), true, t.number % 5 = 0, 'Tags',
    timestamptz '2025-03-01 00:00:00+00', timestamptz '2025-06-01 00:00:00+00', 0,
    '019a0000-0000-7000-8000-000000000001'::uuid
FROM (
    SELECT coalesce(s.folder, replace(coalesce(s.artist, s.track_artist), '/', '_') || '/' || s.album) AS directory,
        lpad(n::text, 2, '0') || ' ' || coalesce(replace(s.track_artist, '/', '_'), s.album) || ' - Song ' || n
            AS base_name,
        n AS number, s.album, s.track_artist, s.genre,
        CASE WHEN s.track_artist IS NULL THEN NULL ELSE md5('artist:' || s.track_artist)::uuid END AS artist_id,
        CASE WHEN s.album IS NULL THEN NULL
            ELSE md5('album:' || coalesce(s.artist, s.folder) || '|' || s.album)::uuid END AS album_id
    FROM seed_tracks AS s, generate_series(1, s.songs) AS n
) AS t;

-- Profiles.
INSERT INTO users (id, name, normalized_name, initials, color, role, is_built_in, created_at)
VALUES
    (md5('user:Anna')::uuid, 'Anna', 'ANNA', 'A', 'blue', 'User', false, timestamptz '2025-01-01 00:00:00+00'),
    (md5('user:Ben')::uuid, 'Ben', 'BEN', 'B', 'green', 'User', false, timestamptz '2025-01-01 00:00:00+00'),
    (md5('user:Chris')::uuid, 'Chris', 'CHRIS', 'C', 'red', 'Admin', false, timestamptz '2025-01-01 00:00:00+00');

-- Favorites of artists and albums that the upgrade joins, partly both of a pair.
INSERT INTO favorites (id, user_id, type, artist_id, created_at)
SELECT md5('favorite:' || f.u || '|' || f.name)::uuid, md5('user:' || f.u)::uuid, 'Artist',
    md5('artist:' || f.name)::uuid, timestamptz '2025-04-01 00:00:00+00'
FROM (VALUES ('Anna', 'K Flay'), ('Anna', 'K.Flay'), ('Anna', 'Beyonce'), ('Ben', 'AC-DC'),
    ('Chris', 'Motley Crue'), ('Chris', 'Guns N Roses'), ('Chris', 'Sigur Rós'), ('Ben', 'Prince')) AS f(u, name);
INSERT INTO favorites (id, user_id, type, album_id, created_at)
SELECT md5('favorite:' || f.u || '|' || f.album)::uuid, md5('user:' || f.u)::uuid, 'Album',
    md5('album:' || f.album)::uuid, timestamptz '2025-04-02 00:00:00+00'
FROM (VALUES ('Anna', 'K Flay|Solutions'), ('Anna', 'Beyonce|Lemonade'), ('Ben', 'AC/DC|Back in Black'),
    ('Ben', 'AC-DC|Back In Black'), ('Chris', 'Motley Crue|Dr Feelgood'), ('Chris', 'Mixes/One|MIX!'),
    ('Chris', 'Simon and Garfunkel|Greatest Hits')) AS f(u, album);
INSERT INTO favorites (id, user_id, type, track_id, created_at)
SELECT md5('favorite:' || u.name || '|' || t.id)::uuid, u.id, 'Track', t.id, timestamptz '2025-04-03 00:00:00+00'
FROM users AS u
JOIN LATERAL (SELECT id FROM tracks ORDER BY md5(id::text || u.name) LIMIT 12) AS t ON true;

-- Ratings and history.
INSERT INTO track_ratings (user_id, track_id, rating, updated_at)
SELECT u.id, t.id, 1 + abs(hashtext(t.id::text || u.name)) % 5, timestamptz '2025-05-01 00:00:00+00'
FROM users AS u
JOIN LATERAL (SELECT id FROM tracks ORDER BY md5(u.name || id::text) LIMIT 30) AS t ON true;
INSERT INTO play_history (id, user_id, track_id, played_at)
SELECT md5('play:' || u.name || '|' || n)::uuid, u.id,
    (SELECT id FROM tracks ORDER BY md5(id::text || u.name || n) LIMIT 1),
    timestamptz '2025-01-01 00:00:00+00' + make_interval(hours => n * 7)
FROM users AS u, generate_series(1, 150) AS n;

-- Playlists with songs of the joined artists and albums.
INSERT INTO playlists (id, user_id, name, normalized_name, created_at, updated_at)
VALUES
    (md5('playlist:Road Trip')::uuid, md5('user:Anna')::uuid, 'Road Trip', 'ROAD TRIP',
        timestamptz '2025-04-01 00:00:00+00', timestamptz '2025-04-01 00:00:00+00'),
    (md5('playlist:Workout')::uuid, md5('user:Ben')::uuid, 'Workout', 'WORKOUT',
        timestamptz '2025-04-01 00:00:00+00', timestamptz '2025-04-01 00:00:00+00');
INSERT INTO playlist_entries (id, playlist_id, track_id, position, added_at)
SELECT md5('entry:Road Trip|' || row_number() OVER (ORDER BY t.id))::uuid, md5('playlist:Road Trip')::uuid, t.id,
    row_number() OVER (ORDER BY t.id) - 1, timestamptz '2025-04-01 00:00:00+00'
FROM tracks AS t
WHERE t.artist_id IN (md5('artist:K Flay')::uuid, md5('artist:Beyonce')::uuid, md5('artist:KDA')::uuid);
INSERT INTO playlist_entries (id, playlist_id, track_id, position, added_at)
SELECT md5('entry:Workout|' || row_number() OVER (ORDER BY t.id))::uuid, md5('playlist:Workout')::uuid, t.id,
    row_number() OVER (ORDER BY t.id) - 1, timestamptz '2025-04-01 00:00:00+00'
FROM tracks AS t
WHERE t.album_id IN (
    md5('album:AC-DC|Back In Black')::uuid,
    md5('album:Guns N Roses|Appetite For Destruction')::uuid);

-- Smart playlists whose rules name artists, albums and genres by text.
INSERT INTO smart_playlists (id, user_id, name, normalized_name, match, rules, sort, direction, "limit", created_at,
    updated_at)
SELECT md5('smart:' || s.name)::uuid, md5('user:' || s.u)::uuid, s.name, upper(s.name), s.match, s.rules::jsonb,
    'Artist', 'Asc', NULL, timestamptz '2025-04-01 00:00:00+00', timestamptz '2025-04-01 00:00:00+00'
FROM (VALUES
    ('Anna', 'Only K.Flay', 'All', '[{"field":"Artist","operator":"Is","text":"K.Flay"}]'),
    ('Anna', 'Lower Case', 'All', '[{"field":"Artist","operator":"Is","text":"kflay"}]'),
    ('Anna', 'Not Beyonce', 'All', '[{"field":"Artist","operator":"IsNot","text":"Beyonce"}]'),
    ('Ben', 'Solutions', 'All', '[{"field":"Album","operator":"Is","text":"Solutions"}]'),
    ('Ben', 'Hip Hop', 'Any',
        '[{"field":"Genre","operator":"Is","text":"hip hop"},{"field":"Genre","operator":"Is","text":"K-Pop"}]'),
    ('Chris', 'Greatest', 'All', '[{"field":"Album","operator":"Is","text":"Greatest Hits"}]'),
    ('Chris', 'Unknown', 'All', '[{"field":"Artist","operator":"Is","text":"Nobody Known"}]'),
    ('Chris', 'Contains', 'All',
        '[{"field":"Artist","operator":"Contains","text":"Beyon"},{"field":"Rating","operator":"AtLeast","number":3}]')
) AS s(u, name, match, rules);

-- Pictures, online covers and players of joined entries.
INSERT INTO artist_pictures (id, artist_id, kind, created_at, width, height)
VALUES (md5('picture:K Flay')::uuid, md5('artist:K Flay')::uuid, 'Uploaded', timestamptz '2025-04-01 00:00:00+00',
    600, 600);
INSERT INTO artist_pictures (id, artist_id, kind, created_at, width, height, provider, author, license)
VALUES (md5('picture:Beyonce')::uuid, md5('artist:Beyonce')::uuid, 'Online', timestamptz '2025-04-01 00:00:00+00',
    800, 800, 'Wikimedia', 'Someone', 'CC BY-SA 4.0');
INSERT INTO online_covers (id, album_id, file_name, mime_type, size, release_id, created_at)
VALUES (md5('online:AC-DC')::uuid, md5('album:AC-DC|Back In Black')::uuid, 'cover.jpg', 'image/jpeg', 1000,
    '1d4a1b46-3c5f-4a3e-9d1d-1a0c6c1b2e3f', timestamptz '2025-04-01 00:00:00+00');
INSERT INTO playback_states (user_id, track_ids, autoplay_positions, current_index, position_milliseconds, shuffle,
    original_order, repeat, source_kind, source_id, device_id, device_name, changed_at)
SELECT md5('user:Anna')::uuid, array_agg(id ORDER BY id), '{}', 0, 1000, false, '{}', 'Off', 'Album',
    md5('album:K Flay|Solutions')::uuid, 'device-1', 'Phone', timestamptz '2025-04-01 00:00:00+00'
FROM tracks WHERE album_id = md5('album:K Flay|Solutions')::uuid;
INSERT INTO playback_states (user_id, track_ids, autoplay_positions, current_index, position_milliseconds, shuffle,
    original_order, repeat, source_kind, source_id, device_id, device_name, changed_at)
SELECT md5('user:Ben')::uuid, array_agg(id ORDER BY id), '{}', 0, 0, false, '{}', 'Off', 'Artist',
    md5('artist:AC-DC')::uuid, 'device-2', 'Laptop', timestamptz '2025-04-01 00:00:00+00'
FROM tracks WHERE artist_id = md5('artist:AC-DC')::uuid;

DROP TABLE seed_albums;
DROP TABLE seed_tracks;
