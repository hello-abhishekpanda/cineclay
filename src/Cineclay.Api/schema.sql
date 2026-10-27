CREATE TABLE IF NOT EXISTS movies (
 id bigint PRIMARY KEY,
 title text NOT NULL,
 release_year integer NOT NULL CHECK (release_year BETWEEN 1990 AND 9999),
 genres text[] NOT NULL,
 payload jsonb NOT NULL,
 updated_at timestamptz NOT NULL DEFAULT now(),
 deleted_at timestamptz
);
CREATE INDEX IF NOT EXISTS movies_year_idx ON movies (release_year) WHERE deleted_at IS NULL;
CREATE INDEX IF NOT EXISTS movies_genres_idx ON movies USING gin(genres);
CREATE INDEX IF NOT EXISTS movies_payload_idx ON movies USING gin(payload);
CREATE TABLE IF NOT EXISTS reviews (
 id uuid PRIMARY KEY, movie_id bigint NOT NULL REFERENCES movies(id), owner_id text NOT NULL,
 rating integer NOT NULL CHECK(rating BETWEEN 1 AND 5),
 text text NOT NULL CHECK(length(text) BETWEEN 1 AND 2000), created_at timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX IF NOT EXISTS reviews_movie_idx ON reviews(movie_id);
CREATE TABLE IF NOT EXISTS watchlist (
 owner_id text NOT NULL,movie_id bigint NOT NULL REFERENCES movies(id),created_at timestamptz NOT NULL DEFAULT now(),
 PRIMARY KEY(owner_id,movie_id)
);
CREATE TABLE IF NOT EXISTS audit_log (
 id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,action text NOT NULL,movie_id bigint,created_at timestamptz NOT NULL DEFAULT now()
);
-- The API owns this database; never expose these tables through an anonymous Supabase client.
ALTER TABLE movies ENABLE ROW LEVEL SECURITY;
ALTER TABLE reviews ENABLE ROW LEVEL SECURITY;
ALTER TABLE watchlist ENABLE ROW LEVEL SECURITY;
ALTER TABLE audit_log ENABLE ROW LEVEL SECURITY;
